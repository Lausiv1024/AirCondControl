using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoolerSystemUI.Services;

namespace CoolerSystemUI.ViewModels
{
    /// <summary>
    /// 画面の消灯/点灯と、無操作タイマー(焼け防止)を管理する。
    /// 消灯中は黒オーバーレイ(MainView)が最前面に表示され、任意の位置のタップで <see cref="Wake"/> される。
    /// </summary>
    public partial class ScreenViewModel : ViewModelBase
    {
        private readonly IScreenPowerController _power;
        private readonly DispatcherTimer? _idleTimer;

        /// <summary>画面が消灯中かどうか。黒オーバーレイの表示にバインドする。</summary>
        [ObservableProperty]
        private bool isScreenOff;

        /// <param name="blankTimeoutSeconds">無操作で自動消灯するまでの秒数。0 以下で自動消灯なし。</param>
        public ScreenViewModel(IScreenPowerController power, int blankTimeoutSeconds)
        {
            _power = power;
            if (blankTimeoutSeconds > 0)
            {
                _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(blankTimeoutSeconds) };
                _idleTimer.Tick += (_, _) => Sleep();
            }
        }

        /// <summary>無操作タイマーを開始する。View の Loaded から呼ぶ。</summary>
        public void Start() => RestartIdleTimer();

        /// <summary>
        /// ユーザ操作があったときに呼ぶ。点灯中はタイマーをリセットする。
        /// 消灯中は何もしない(復帰はオーバーレイのタップ → <see cref="Wake"/> が担う)。
        /// </summary>
        public void ResetActivity()
        {
            if (IsScreenOff)
                return;
            RestartIdleTimer();
        }

        /// <summary>画面を消灯する(手動の消灯ボタン / 無操作タイマーの両方から呼ばれる)。</summary>
        [RelayCommand]
        public void Sleep()
        {
            if (IsScreenOff)
                return;
            _idleTimer?.Stop();
            _power.Sleep();
            IsScreenOff = true;
        }

        /// <summary>画面を点灯し、無操作タイマーを再開する。</summary>
        public void Wake()
        {
            if (IsScreenOff)
            {
                _power.Wake();
                IsScreenOff = false;
            }
            RestartIdleTimer();
        }

        private void RestartIdleTimer()
        {
            if (_idleTimer == null)
                return;
            _idleTimer.Stop();
            _idleTimer.Start();
        }
    }
}
