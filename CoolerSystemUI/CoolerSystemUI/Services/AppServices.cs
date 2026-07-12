using System;
using CoolerSystemUI.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoolerSystemUI.Services
{
    /// <summary>
    /// DI コンテナを構築する。appsettings.json と環境変数から API 接続先を解決する。
    /// </summary>
    public static class AppServices
    {
        public static IServiceProvider Build()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            // 接続先 URL: 環境変数 COOLER_API_BASEURL > appsettings(Api:BaseUrl) > 既定。
            var baseUrl = Environment.GetEnvironmentVariable("COOLER_API_BASEURL")
                          ?? config["Api:BaseUrl"]
                          ?? "http://localhost:5195";
            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
                baseUrl += "/";

            var timeoutSeconds = int.TryParse(config["Api:TimeoutSeconds"], out var t) && t > 0 ? t : 10;

            // 自動消灯までの秒数: 環境変数 COOLER_BLANK_TIMEOUT > appsettings(Ui:BlankTimeoutSeconds) > 60。0 で自動消灯なし。
            var blankTimeout = 60;
            var rawTimeout = Environment.GetEnvironmentVariable("COOLER_BLANK_TIMEOUT") ?? config["Ui:BlankTimeoutSeconds"];
            if (int.TryParse(rawTimeout, out var bt) && bt >= 0)
                blankTimeout = bt;

            // バックライト sysfs ディレクトリ: 環境変数 COOLER_BACKLIGHT > appsettings(Ui:BacklightPath) > 自動検出。
            var backlightDir = Environment.GetEnvironmentVariable("COOLER_BACKLIGHT") ?? config["Ui:BacklightPath"];

            // 自動送信のデバウンス時間(ms): 環境変数 > appsettings(Ui:AutoSendDebounceMs) > 800。
            var debounceMs = 800;
            if (int.TryParse(Environment.GetEnvironmentVariable("COOLER_AUTOSEND_DEBOUNCE_MS") ?? config["Ui:AutoSendDebounceMs"], out var dm) && dm > 0)
                debounceMs = dm;

            // サーバ同期ポーリング間隔(秒): 環境変数 > appsettings(Ui:PollIntervalSeconds) > 5。0 でポーリングなし。
            var pollSeconds = 5;
            if (int.TryParse(Environment.GetEnvironmentVariable("COOLER_POLL_INTERVAL") ?? config["Ui:PollIntervalSeconds"], out var ps) && ps >= 0)
                pollSeconds = ps;

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);

            services.AddHttpClient<IAircondApiClient, AircondApiClient>(c =>
            {
                c.BaseAddress = new Uri(baseUrl);
                c.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });

            services.AddSingleton<IScreenPowerController>(new ScreenPowerController(backlightDir));
            services.AddSingleton<ScreenViewModel>(sp =>
                new ScreenViewModel(sp.GetRequiredService<IScreenPowerController>(), blankTimeout));

            services.AddSingleton<AircondViewModel>(sp =>
                new AircondViewModel(sp.GetRequiredService<IAircondApiClient>(), debounceMs, pollSeconds));
            services.AddSingleton<CirculatorViewModel>();
            services.AddSingleton<AutomationViewModel>();
            services.AddSingleton<MainViewModel>();

            return services.BuildServiceProvider();
        }
    }
}
