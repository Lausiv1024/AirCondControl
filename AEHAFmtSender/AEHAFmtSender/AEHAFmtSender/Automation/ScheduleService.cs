using AEHAFmtSender.IRFormats;
using AEHAFmtSender.Shared.Models;
using Microsoft.Extensions.Logging;
using SharedModels = AEHAFmtSender.Shared.Models;

namespace AEHAFmtSender.Automation;

/// <summary>
/// スケジュールの発火と、サーバー側タイマーのカウントダウンを回す。
/// エアコン内蔵のタイマーは使わず、満了時に自分で OFF/ON 信号を送る。
/// </summary>
public sealed class ScheduleService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 長時間停止していた場合に、溜まった過去のスケジュールを一斉発火させないための上限。
    /// タイマーの満了はこの制限を受けない (下記 RunCountdownsAsync 参照)。
    /// </summary>
    private static readonly TimeSpan CatchUpWindow = TimeSpan.FromMinutes(15);

    private readonly AircondService _aircond;
    private readonly ScheduleConfigManager _config;
    private readonly ScheduleStateManager _state;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
        AircondService aircond,
        ScheduleConfigManager config,
        ScheduleStateManager state,
        ILogger<ScheduleService> logger)
    {
        _aircond = aircond;
        _config = config;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync();
            }
            catch (Exception ex)
            {
                // 1 件の不正なルールでサービスごと止めない。
                _logger.LogError(ex, "スケジュールの処理に失敗しました");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync()
    {
        var nowUtc = DateTime.UtcNow;
        var now = nowUtc.ToLocalTime();

        await RunCountdownsAsync(nowUtc);

        if (_config.Config.Enabled)
        {
            // 前回評価からの区間を見る。初回は区間なし (起動直後に過去分を撃たない)。
            var from = _state.State.LastTickUtc?.ToLocalTime() ?? now;
            var earliest = now - CatchUpWindow;
            if (from < earliest)
            {
                _logger.LogInformation("{Gap} 停止していたため、古いスケジュールはスキップします", now - from);
                from = earliest;
            }

            foreach (var fire in ScheduleEvaluator.DueRules(_config.Config.Rules, from, now))
                await ApplyRuleAsync(fire);
        }

        _state.Update(s => s.LastTickUtc = nowUtc);
    }

    /// <summary>
    /// タイマーの満了確認。スケジュールと違い、遅れていても必ず送る。
    /// 復帰した時点でエアコンは点いたままなので、遅れてでも OFF を送る方が安全側。
    /// </summary>
    private async Task RunCountdownsAsync(DateTime nowUtc)
    {
        if (_state.State.OffAtUtc is DateTime offAt && nowUtc >= offAt)
        {
            _logger.LogInformation("切タイマー満了 (予定 {OffAt})", offAt.ToLocalTime());
            await SetPowerAsync(false);
        }

        if (_state.State.OnAtUtc is DateTime onAt && nowUtc >= onAt)
        {
            _logger.LogInformation("入タイマー満了 (予定 {OnAt})", onAt.ToLocalTime());
            await SetPowerAsync(true);
        }
    }

    private async Task SetPowerAsync(bool power)
    {
        var next = AircondService.Copy(_aircond.Current);
        next.Power = power;
        // タイマーは使い切ったので解除する。AircondService 側で OffAtUtc / OnAtUtc も消える。
        next.TimerMode = IRFormats.TimerMode.NONE;
        next.TimerLength = 0;

        await _aircond.ApplyAsync(next);
    }

    private async Task ApplyRuleAsync(ScheduledFire fire)
    {
        var rule = fire.Rule;
        var next = AircondService.Copy(_aircond.Current);

        if (rule.Power is bool power)
            next.Power = power;

        if (rule.OperationMode is SharedModels.OperationMode mode)
            next.OperationMode = (IRFormats.OperationMode)(int)mode;

        if (rule.Dehumidification is SharedModels.DehumidificationAdjustments dehumid)
            next.Dehumidification = (IRFormats.DehumidificationAdjustments)(int)dehumid;

        if (rule.Degrees is int degrees)
        {
            // 除湿・送風には設定温度がなく、代入すると setter が例外を投げる。
            if (next.OperationMode is IRFormats.OperationMode.COOLING or IRFormats.OperationMode.HEATING)
                next.Degrees = degrees;
            else
                _logger.LogInformation(
                    "ルール {Name} の設定温度は {Mode} 運転のためスキップしました", rule.Name, next.OperationMode);
        }

        // 指定がなければ現在のタイマー設定を引き継ぐ (Copy 済みの値のまま)。
        if (rule.OffAfterMinutes is int minutes && minutes > 0)
        {
            next.TimerMode = IRFormats.TimerMode.OFFTIMER;
            next.TimerLength = minutes;
        }

        _logger.LogInformation("スケジュール {Name} を適用します ({FiresAt})", rule.Name, fire.FiresAt);
        await _aircond.ApplyAsync(next);

        // 単発予約は撃ち切り。
        if (rule.Date is not null)
        {
            rule.Enabled = false;
            _config.Save();
        }
    }
}
