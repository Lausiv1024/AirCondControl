namespace CoolerSystemWebApi;

/// <summary>
/// RPi 上の AEHAFmtSender へ中継するための設定 (appsettings.json の "RpiProxy" セクション)。
/// </summary>
public sealed class RpiProxyOptions
{
    public const string SectionName = "RpiProxy";

    /// <summary>
    /// RPi 上の AEHAFmtSender のベース URL。例: http://192.168.1.50:5195
    /// (RPi 側 aehafmtsender.service の既定は 127.0.0.1:5195 なので、LAN から叩くには
    ///  ASPNETCORE_URLS を 0.0.0.0:5195 に変更しておくこと。)
    /// </summary>
    public string BaseUrl { get; set; } = "http://192.168.1.22";

    /// <summary>RPi への 1 リクエストのタイムアウト (秒)。IR 送信は irsend の完了待ちで数秒かかる。</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// 中継を許可するパス。"METHOD path" 形式 (例: "POST apiac")。
    /// 空にすると全パスを素通しする (Cloudflare Access の内側なので運用次第)。
    /// </summary>
    public string[] AllowedRoutes { get; set; } =
    [
        "GET acget",
        "POST apiac",
        "POST simplecode",
        "GET circulatorconfig",
        "POST circulatorconfig",
        "POST circulatorconfig/reload",
        "GET automationconfig",
        "POST automationconfig",
        "GET sensordata/latest",
        "GET sensordata/history",
    ];

    /// <summary>
    /// 指定のメソッド・パスが中継対象か。パスは前後のスラッシュを無視し、大文字小文字を区別しない。
    /// </summary>
    public bool IsAllowed(string method, string path)
    {
        if (AllowedRoutes.Length == 0)
            return true;

        var normalized = $"{method.ToUpperInvariant()} {path.Trim('/')}";
        return AllowedRoutes.Any(r =>
            string.Equals(Normalize(r), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string route)
    {
        var parts = route.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            ? $"{parts[0].ToUpperInvariant()} {parts[1].Trim('/')}"
            : route.Trim();
    }
}
