using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoolerSystemUI.ViewModels
{
    /// <summary>
    /// 画面全体のルート。3 つのタブ (エアコン / サーキュレーター / 設定) を束ねる。
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        public AircondViewModel Aircond { get; }
        public CirculatorViewModel Circulator { get; }
        public AutomationViewModel Automation { get; }

        /// <summary>タブバー右に出す「次に実行される予定」(読み取り専用)。</summary>
        public NextScheduleViewModel NextSchedule { get; }

        /// <summary>画面消灯/点灯と無操作タイマーを管理する。</summary>
        public ScreenViewModel Screen { get; }

        [ObservableProperty]
        private int selectedTabIndex;

        public MainViewModel(
            AircondViewModel aircond,
            CirculatorViewModel circulator,
            AutomationViewModel automation,
            NextScheduleViewModel nextSchedule,
            ScreenViewModel screen)
        {
            Aircond = aircond;
            Circulator = circulator;
            Automation = automation;
            NextSchedule = nextSchedule;
            Screen = screen;
        }

        /// <summary>起動時にサーバから現在状態を読み込む。View の Loaded から呼ぶ。</summary>
        [RelayCommand]
        private async Task InitializeAsync()
        {
            await Aircond.LoadCommand.ExecuteAsync(null);
            await Automation.LoadCommand.ExecuteAsync(null);
            await NextSchedule.LoadCommand.ExecuteAsync(null);
        }
    }
}
