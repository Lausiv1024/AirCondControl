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
    private readonly ILogger<AircondService> _logger;

    public AircondService(
        AircondConfigManager<NP081> aircondConfig,
        AutomationConfigManager automationConfig,
        ILogger<AircondService> logger)
    {
        _aircondConfig = aircondConfig;
        _automationConfig = automationConfig;
        _logger = logger;
    }

    /// <summary>保存されている現在の状態。未保存なら既定値。</summary>
    public NP081 Current => _aircondConfig.controller ?? new NP081();

    /// <summary>タイマーを開始した時刻。満了判定の起点になる。</summary>
    public DateTime TimerStarted { get; private set; } = DateTime.Now;

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

        var signal = next.GetCurrentSignal();
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

            if (timerChanged && next.TimerMode != TimerMode.NONE)
                TimerStarted = DateTime.Now;
        }

        _aircondConfig.controller = next;
        _aircondConfig.Save();
    }
}
