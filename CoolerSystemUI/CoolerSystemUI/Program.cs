using Avalonia;
using System;
using System.Globalization;
using System.Linq;

namespace CoolerSystemUI
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            var builder = BuildAvaloniaApp();

            // ラズパイ実機ではキオスク(Linux DRM)で起動する。
            // 有効化条件: 起動引数に --drm を付与、または環境変数 COOLER_DRM=1。
            if (UseDrm(args))
            {
                // card 例: /dev/dri/card0 (未指定なら Avalonia が既定カードを選択)
                var card = Environment.GetEnvironmentVariable("COOLER_DRI_CARD");
                var scaling = ParseScaling(Environment.GetEnvironmentVariable("COOLER_DRM_SCALING"));
                builder.StartLinuxDrm(args, card, scaling);
            }
            else
            {
                builder.StartWithClassicDesktopLifetime(args);
            }
        }

        private static bool UseDrm(string[] args)
            => args.Contains("--drm")
               || string.Equals(Environment.GetEnvironmentVariable("COOLER_DRM"), "1", StringComparison.Ordinal);

        private static double ParseScaling(string? value)
            => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s > 0
                ? s
                : 1.0;

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
