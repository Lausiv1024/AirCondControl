using AEHAFmtSender.IRFormats;
using Microsoft.Extensions.Logging;

namespace AEHAFmtSender.Automation;

/// <summary>
/// エアコンの状態変更と赤外線送信の唯一の経路。
///
/// lircd への送信は conf の書き換えと <c>systemctl restart lircd</c> を伴うため、
/// 同時に走ると conf の内容と再起動が競合して信号が壊れる。HTTP からの操作と
/// バックグラウンド (タイマー・スケジュール) からの操作を同じセマフォで直列化する。
/// サーキュレーターも同じ lircd を共有するので、その送信と conf 生成もここに通す。
/// </summary>
public sealed class AircondService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AircondConfigManager<NP081> _aircondConfig;
    private readonly AutomationConfigManager _automationConfig;
    private readonly ScheduleStateManager _scheduleState;
    private readonly ILogger<AircondService> _logger;

    public AircondService(
        AircondConfigManager<NP081> aircondConfig,
        AutomationConfigManager automationConfig,
        ScheduleStateManager scheduleState,
        ILogger<AircondService> logger)
    {
        _aircondConfig = aircondConfig;
        _automationConfig = automationConfig;
        _scheduleState = scheduleState;
        _logger = logger;
    }

    /// <summary>保存されている現在の状態。未保存なら既定値。</summary>
    public NP081 Current => _aircondConfig.controller ?? new NP081();

    /// <summary>
    /// 状態を複製する。
    ///
    /// JSON 経由で複製すると <see cref="NP081.Degrees"/> が
    /// <see cref="NP081.OperationMode"/> より先に代入され、冷房以外のときに
    /// 設定温度が壊れる (あるいは setter が例外を投げる)。
    /// 運転モードを先に決める順序で手書きする。
    /// </summary>
    public static NP081 Copy(NP081 src) => new()
    {
        OperationMode = src.OperationMode,
        Power = src.Power,
        CoolingDegrees = src.CoolingDegrees,
        Heatingdegrees = src.Heatingdegrees,
        TimerMode = src.TimerMode,
        TimerLength = src.TimerLength,
        Dehumidification = src.Dehumidification,
    };

    /// <summary>
    /// 状態を適用し、信号を送って保存する。電源連動のサーキュレーター送信もここで行う。
    /// </summary>
    public async Task ApplyAsync(NP081 next)
    {
        await _gate.WaitAsync();
        try
        {
            await ApplyCoreAsync(next);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>サーキュレーター単体の送信。</summary>
    public async Task SendCirculatorAsync(string? id)
    {
        await _gate.WaitAsync();
        try
        {
            await IrSending.sendCirculatorSignal(id);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>circulator.conf を生成・配置する。内容が変わったときだけ lircd を再起動する。</summary>
    public async Task EnsureCirculatorConfAsync(CirculatorConfig config)
    {
        await _gate.WaitAsync();
        try
        {
            await IrSending.EnsureCirculatorConf(config);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ApplyCoreAsync(NP081 next)
    {
        var previous = _aircondConfig.controller;

        // 電源を切るなら切タイマーは無意味なので落とす。入タイマーは「今は消えていて
        // 後で点く」状態なので残す。
        if (!next.Power && next.TimerMode == TimerMode.OFFTIMER)
        {
            next.TimerMode = TimerMode.NONE;
            next.TimerLength = 0;
        }

        // 入タイマー中は電源が入っていてはいけない。UI は電源とタイマー種別を独立に
        // 送ってくるので、電源 ON のまま入タイマーを設定されることがある。信号から
        // タイマーを外している以上、ここで落とさないとエアコンがその場で点いてしまう。
        if (next.TimerMode == TimerMode.ONTIMER)
            next.Power = false;

        // 信号にはタイマーを載せない。NP081 は毎回全状態を送るので、タイマーを載せると
        // 温度を変えるたびにエアコン側のタイマーが振り出しに戻ってしまう。
        // カウントダウンはこちらで持ち、満了時に OFF/ON を送る。
        var forSignal = Copy(next);
        forSignal.TimerMode = TimerMode.NONE;
        forSignal.TimerLength = 0;

        var signal = forSignal.GetCurrentSignal();
        _logger.LogDebug("エアコンへ送信: {Signal}", Convert.ToHexString(signal));
        await IrSending.SendByte(signal);

        if (previous != null)
        {
            var powerChanged = next.PowerStateChanged(previous);
            var timerChanged = next.TimerStatusChanged(previous);

            // 入タイマーの開始・解除はエアコンの電源が入る/入らなくなるタイミングなので、
            // 電源の変化と同じ扱いでサーキュレーターも合わせて動かす。
            if (_automationConfig.Config.AircondPwrLink
                && (powerChanged
                    || (timerChanged
                        && (next.TimerMode == TimerMode.ONTIMER || previous.TimerMode == TimerMode.ONTIMER))))
            {
                // 既にゲートを保持しているので SendCirculatorAsync は呼べない
                // (SemaphoreSlim は再入不可でデッドロックする)。
                await IrSending.sendCirculatorSignal("power");
            }
        }

        UpdateCountdown(next, previous);

        _aircondConfig.controller = next;
        _aircondConfig.Save();
    }

    /// <summary>
    /// タイマーの指定が変わったときだけカウントダウンを張り直す。
    ///
    /// 温度や運転モードだけを変えた場合は指定が変わらないので、残り時間は据え置かれる。
    /// これが「設定変更でタイマーが狂わない」の実体。
    /// </summary>
    private void UpdateCountdown(NP081 next, NP081? previous)
    {
        var intentChanged = previous == null
            || next.TimerMode != previous.TimerMode
            || next.TimerLength != previous.TimerLength;

        if (!intentChanged)
            return;

        var firesAt = DateTime.UtcNow.AddMinutes(next.TimerLength);
        _scheduleState.Update(s =>
        {
            s.OffAtUtc = next.TimerMode == TimerMode.OFFTIMER ? firesAt : null;
            s.OnAtUtc = next.TimerMode == TimerMode.ONTIMER ? firesAt : null;
        });

        if (next.TimerMode != TimerMode.NONE)
            _logger.LogInformation("{Mode} を {FiresAt} に設定しました", next.TimerMode, firesAt.ToLocalTime());
    }
}
