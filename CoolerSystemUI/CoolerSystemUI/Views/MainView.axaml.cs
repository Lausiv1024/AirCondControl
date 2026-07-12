using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CoolerSystemUI.ViewModels;

namespace CoolerSystemUI.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (DataContext is not MainViewModel vm)
                return;

            // 起動時にサーバから現在状態を読み込む。
            if (vm.InitializeCommand.CanExecute(null))
                vm.InitializeCommand.Execute(null);

            // 画面全体の操作を監視して無操作タイマーをリセットする。
            // トンネルで TopLevel に登録するため、配下のどのコントロールへの操作も検知できる。
            var top = TopLevel.GetTopLevel(this);
            if (top != null)
            {
                top.AddHandler(PointerPressedEvent, OnGlobalPointer, RoutingStrategies.Tunnel);
                top.AddHandler(PointerMovedEvent, OnGlobalPointer, RoutingStrategies.Tunnel);
            }

            // 無操作タイマー開始。
            vm.Screen.Start();
        }

        // 何らかのポインタ操作 → 無操作タイマーをリセット(消灯中は ScreenViewModel 側で無視される)。
        private void OnGlobalPointer(object? sender, PointerEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.Screen.ResetActivity();
        }

        // 黒オーバーレイのタップ → 点灯。背後の UI を誤操作しないよう、このイベントはここで止める。
        private void OnOverlayPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.Screen.Wake();
            e.Handled = true;
        }
    }
}
