using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CoolerSystemUI.Services;
using CoolerSystemUI.ViewModels;
using CoolerSystemUI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoolerSystemUI
{
    public partial class App : Application
    {
        /// <summary>アプリ全体の DI コンテナ。</summary>
        public static IServiceProvider Services { get; private set; } = default!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Services = AppServices.Build();
            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            var rotation = GetRotation();

            switch (ApplicationLifetime)
            {
                // デスクトップ (Windows/macOS、開発時): ウィンドウで表示
                case IClassicDesktopStyleApplicationLifetime desktop:
                    desktop.MainWindow = new MainWindow { DataContext = mainViewModel };
                    break;

                // Linux DRM (キオスク、ラズパイ実機): シングルビューを直接マウント
                // パネルの取り付け向きに合わせてルートを回転 (描画とタッチ座標の両方が補正される)
                case ISingleViewApplicationLifetime singleView:
                    var view = new MainView { DataContext = mainViewModel };
                    singleView.MainView = WrapWithRotation(view, rotation);
                    break;
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// 画面回転角(度)を取得する。環境変数 COOLER_DRM_ROTATE > appsettings(Ui:Rotate) > 0。
        /// 0 / 90 / 180 / 270 のみ有効。
        /// </summary>
        private static int GetRotation()
        {
            var raw = Environment.GetEnvironmentVariable("COOLER_DRM_ROTATE")
                      ?? Services.GetService<IConfiguration>()?["Ui:Rotate"];
            if (int.TryParse(raw, out var deg))
            {
                deg = ((deg % 360) + 360) % 360;
                if (deg is 0 or 90 or 180 or 270)
                    return deg;
            }
            return 0;
        }

        /// <summary>
        /// 指定角度でルートを回転させるラッパを返す。0 度ならそのまま返す。
        /// LayoutTransform を使うため 90/270 度でも縦横が正しく再レイアウトされる。
        /// </summary>
        private static Control WrapWithRotation(Control content, int degrees)
        {
            if (degrees % 360 == 0)
                return content;

            return new LayoutTransformControl
            {
                LayoutTransform = new RotateTransform(degrees),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = content,
            };
        }
    }
}
