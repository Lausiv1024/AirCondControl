using CoolerSystemWebApi;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RpiProxyOptions>(builder.Configuration.GetSection(RpiProxyOptions.SectionName));

builder.Services.AddHttpClient(RpiProxy.HttpClientName, (sp, http) =>
{
    var opts = sp.GetRequiredService<IOptions<RpiProxyOptions>>().Value;

    // BaseAddress を相対パス結合の基準にするため、末尾スラッシュを保証する。
    var baseUrl = opts.BaseUrl.TrimEnd('/') + "/";
    http.BaseAddress = new Uri(baseUrl);
    http.Timeout = TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds));
});

var app = builder.Build();

// Cloudflare Tunnel (cloudflared) が同一ホストから平文 HTTP で叩く前提。
// TLS 終端と認証 (Cloudflare Access) は Cloudflare 側が担当するので、ここでは HTTPS リダイレクトも認証も行わない。

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapRpiProxy();

// Tunnel / 監視用。RPi への疎通は見ないので、このプロセスの生存確認のみ。
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// SPA のクライアントサイドルーティング用フォールバック。
app.MapFallbackToFile("index.html");

app.Run();
