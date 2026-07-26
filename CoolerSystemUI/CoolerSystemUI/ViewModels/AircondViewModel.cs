using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using AEHAFmtSender.Shared.Models;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoolerSystemUI.Services;

namespace CoolerSystemUI.ViewModels
{
    /// <summary>
    /// エアコン本体の操作 (電源 / 運転モード / 設定温度 / タイマー)。
    ///
    /// 実機リモコンに近い操作感にするため、次の同期方式をとる:
    ///  - 自動送信: 値を変更すると一定時間 (デバウンス) 操作が止まった時点で自動的に <c>POST /apiac</c>。連打中は送信しない。
    ///  - ポーリング同期: 一定間隔で <c>GET /acget</c> し、サーバ状態が変わっていて編集中でなければ UI に反映。
    ///  - エコー防止: Load / ポーリングでの反映中 (<see cref="_suspendAutoSend"/>) は自動送信をトリガしない。
    /// </summary>
    public partial class AircondViewModel : ViewModelBase
    {
        private readonly IAircondApiClient _api;

        private const int MinDegree = 16;
        private const int MaxDegree = 31;
        private const int MinTimer = 30;
        private const int MaxTimer = 720;
        private const int TimerStep = 30;

        // 自動送信のトリガ対象となる「モデル」プロパティ名。計算プロパティや IsBusy 等は対象外。
        private static readonly HashSet<string> SendableProps = new()
        {
            nameof(Power), nameof(CoolingDegrees), nameof(HeatingDegrees),
            nameof(OperationMode), nameof(TimerMode), nameof(TimerLength),
            nameof(Dehumidification),
        };

        private readonly DispatcherTimer _debounce;   // 無操作後の自動送信用
        private readonly DispatcherTimer? _poll;       // サーバ状態のポーリング用

        private bool _suspendAutoSend;                 // true の間はプロパティ変更で自動送信しない (反映中)
        private bool _sendPending;                     // 送信予定 / 送信中 (ポーリング反映を抑止)

        /// <param name="autoSendDebounceMs">操作が止まってから自動送信するまでの待ち時間 (ms)。</param>
        /// <param name="pollIntervalSeconds">サーバ状態をポーリングする間隔 (秒)。0 以下でポーリングなし。</param>
        public AircondViewModel(IAircondApiClient api, int autoSendDebounceMs, int pollIntervalSeconds)
        {
            _api = api;

            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(100, autoSendDebounceMs)) };
            _debounce.Tick += OnDebounceTick;

            if (pollIntervalSeconds > 0)
            {
                _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(pollIntervalSeconds) };
                _poll.Tick += OnPollTick;
                _poll.Start();
            }
        }

        [ObservableProperty]
        private bool power;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayDegree))]
        [NotifyPropertyChangedFor(nameof(TemperatureText))]
        private int coolingDegrees = 28;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayDegree))]
        [NotifyPropertyChangedFor(nameof(TemperatureText))]
        private int heatingDegrees = 20;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCooling))]
        [NotifyPropertyChangedFor(nameof(IsDehumidification))]
        [NotifyPropertyChangedFor(nameof(IsHeating))]
        [NotifyPropertyChangedFor(nameof(IsVentilation))]
        [NotifyPropertyChangedFor(nameof(CanEditTemperature))]
        [NotifyPropertyChangedFor(nameof(ShowTemperature))]
        [NotifyPropertyChangedFor(nameof(DisplayDegree))]
        [NotifyPropertyChangedFor(nameof(TemperatureText))]
        private OperationMode operationMode = OperationMode.COOLING;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDehumidStrong))]
        [NotifyPropertyChangedFor(nameof(IsDehumidNormal))]
        [NotifyPropertyChangedFor(nameof(IsDehumidWeak))]
        private DehumidificationAdjustments dehumidification = DehumidificationAdjustments.NORMAL;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTimerNone))]
        [NotifyPropertyChangedFor(nameof(IsOffTimer))]
        [NotifyPropertyChangedFor(nameof(IsOnTimer))]
        [NotifyPropertyChangedFor(nameof(CanEditTimer))]
        [NotifyPropertyChangedFor(nameof(TimerText))]
        private TimerMode timerMode = TimerMode.NONE;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TimerText))]
        private int timerLength = 30;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        // --- 運転モードの選択状態 (トグルボタンのハイライト用) ---
        public bool IsCooling => OperationMode == OperationMode.COOLING;
        public bool IsDehumidification => OperationMode == OperationMode.DEHUMIDIFICATION;
        public bool IsHeating => OperationMode == OperationMode.HEATING;
        public bool IsVentilation => OperationMode == OperationMode.VENTILATION;

        // --- タイマーモードの選択状態 ---
        public bool IsTimerNone => TimerMode == TimerMode.NONE;
        public bool IsOffTimer => TimerMode == TimerMode.OFFTIMER;
        public bool IsOnTimer => TimerMode == TimerMode.ONTIMER;

        // --- 除湿強度の選択状態 ---
        public bool IsDehumidStrong => Dehumidification == DehumidificationAdjustments.STRONG;
        public bool IsDehumidNormal => Dehumidification == DehumidificationAdjustments.NORMAL;
        public bool IsDehumidWeak => Dehumidification == DehumidificationAdjustments.WEAK;

        /// <summary>温度設定が可能なモード (冷房・暖房) か。</summary>
        public bool CanEditTemperature => OperationMode is OperationMode.COOLING or OperationMode.HEATING;

        /// <summary>設定温度エリアを表示するか。除湿時は代わりに除湿強度エリアを出す。</summary>
        public bool ShowTemperature => OperationMode != OperationMode.DEHUMIDIFICATION;

        /// <summary>現在のモードでの設定温度。</summary>
        public int DisplayDegree => OperationMode == OperationMode.HEATING ? HeatingDegrees : CoolingDegrees;

        public string TemperatureText => CanEditTemperature ? $"{DisplayDegree}℃" : "--";

        public bool CanEditTimer => TimerMode != TimerMode.NONE;

        public string TimerText => CanEditTimer ? $"{TimerLength / 60}時間{TimerLength % 60:00}分" : "--";

        // --- 自動送信のトリガ ---------------------------------------------------

        /// <summary>モデルのプロパティが (ユーザ操作で) 変わったら自動送信をスケジュールする。</summary>
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (_suspendAutoSend)
                return;
            if (e.PropertyName != null && SendableProps.Contains(e.PropertyName))
                ScheduleAutoSend();
        }

        private void ScheduleAutoSend()
        {
            _sendPending = true;
            StatusMessage = "変更を送信予定…";
            _debounce.Stop();
            _debounce.Start();
        }

        private async void OnDebounceTick(object? sender, EventArgs e)
        {
            _debounce.Stop();
            await SendCoreAsync();
        }

        // --- ポーリング同期 -----------------------------------------------------

        private async void OnPollTick(object? sender, EventArgs e)
        {
            // 編集中・送信予定中・送信中はサーバ状態で上書きしない。
            if (_suspendAutoSend || _sendPending || IsBusy)
                return;

            NP081Dto s;
            try
            {
                s = await _api.GetStateAsync();
            }
            catch
            {
                IsConnected = false;
                return;
            }

            // 取得中に編集が始まっていないか再確認。
            if (_suspendAutoSend || _sendPending || IsBusy)
                return;

            IsConnected = true;
            if (StateEquals(s))
                return;

            ApplyServerState(s);
            StatusMessage = "サーバの変更を反映しました";
        }

        // --- コマンド -----------------------------------------------------------

        [RelayCommand]
        private void SetMode(OperationMode mode) => OperationMode = mode;

        [RelayCommand]
        private void SetTimerMode(TimerMode mode) => TimerMode = mode;

        [RelayCommand]
        private void SetDehumidification(DehumidificationAdjustments level) => Dehumidification = level;

        [RelayCommand]
        private void IncrementTemperature()
        {
            if (!CanEditTemperature) return;
            if (OperationMode == OperationMode.HEATING)
                HeatingDegrees = Math.Min(MaxDegree, HeatingDegrees + 1);
            else
                CoolingDegrees = Math.Min(MaxDegree, CoolingDegrees + 1);
        }

        [RelayCommand]
        private void DecrementTemperature()
        {
            if (!CanEditTemperature) return;
            if (OperationMode == OperationMode.HEATING)
                HeatingDegrees = Math.Max(MinDegree, HeatingDegrees - 1);
            else
                CoolingDegrees = Math.Max(MinDegree, CoolingDegrees - 1);
        }

        [RelayCommand]
        private void IncrementTimer()
        {
            if (!CanEditTimer) return;
            TimerLength = Math.Min(MaxTimer, TimerLength + TimerStep);
        }

        [RelayCommand]
        private void DecrementTimer()
        {
            if (!CanEditTimer) return;
            TimerLength = Math.Max(MinTimer, TimerLength - TimerStep);
        }

        /// <summary>サーバから現在状態を取得して UI に反映する。</summary>
        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var s = await _api.GetStateAsync();
                ApplyServerState(s);
                IsConnected = true;
                StatusMessage = "現在の状態を取得しました";
            }
            catch (Exception ex)
            {
                IsConnected = false;
                StatusMessage = $"接続できません: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>現在の設定を即時送信する (デバウンスを待たない手動送信)。</summary>
        [RelayCommand]
        private Task ApplyAsync() => SendCoreAsync();

        // --- 内部ヘルパ ---------------------------------------------------------

        private async Task SendCoreAsync()
        {
            // 送信中に重ねて呼ばれたら少し後に再試行する。
            if (IsBusy)
            {
                _debounce.Start();
                return;
            }

            _debounce.Stop();
            _sendPending = false;
            IsBusy = true;
            try
            {
                await _api.ApplyAsync(BuildDto());
                IsConnected = true;
                StatusMessage = "送信しました";
            }
            catch (Exception ex)
            {
                IsConnected = false;
                StatusMessage = $"送信に失敗しました: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>サーバ状態を UI に反映する。反映中は自動送信を抑止する (エコー防止)。</summary>
        private void ApplyServerState(NP081Dto s)
        {
            _suspendAutoSend = true;
            try
            {
                Power = s.Power;
                CoolingDegrees = Clamp(s.CoolingDegrees, MinDegree, MaxDegree, 28);
                HeatingDegrees = Clamp(s.HeatingDegrees, MinDegree, MaxDegree, 20);
                OperationMode = s.OperationMode;
                TimerMode = s.TimerMode;
                TimerLength = ClampTimer(s.TimerLength);
                Dehumidification = NormalizeDehumid(s.Dehumidification);
            }
            finally
            {
                _suspendAutoSend = false;
            }
        }

        /// <summary>サーバ状態が現在の UI と (クランプ後で) 一致するか。ポーリングの上書き要否判定に使う。</summary>
        private bool StateEquals(NP081Dto s)
            => s.Power == Power
               && Clamp(s.CoolingDegrees, MinDegree, MaxDegree, 28) == CoolingDegrees
               && Clamp(s.HeatingDegrees, MinDegree, MaxDegree, 20) == HeatingDegrees
               && s.OperationMode == OperationMode
               && s.TimerMode == TimerMode
               && ClampTimer(s.TimerLength) == TimerLength
               && NormalizeDehumid(s.Dehumidification) == Dehumidification;

        private NP081Dto BuildDto() => new()
        {
            Power = Power,
            CoolingDegrees = CoolingDegrees,
            HeatingDegrees = HeatingDegrees,
            Degree = DisplayDegree,
            OperationMode = OperationMode,
            TimerMode = TimerMode,
            TimerLength = TimerLength,
            Dehumidification = Dehumidification,
        };

        /// <summary>定義外の値 (古い設定ファイル等) が来たら標準に丸める。</summary>
        private static DehumidificationAdjustments NormalizeDehumid(DehumidificationAdjustments value)
            => value is DehumidificationAdjustments.STRONG
                     or DehumidificationAdjustments.NORMAL
                     or DehumidificationAdjustments.WEAK
                ? value
                : DehumidificationAdjustments.NORMAL;

        private static int Clamp(int value, int min, int max, int fallback)
        {
            if (value < min || value > max) return fallback;
            return value;
        }

        private static int ClampTimer(int value)
            => value < MinTimer ? MinTimer : Math.Min(MaxTimer, value);
    }
}
