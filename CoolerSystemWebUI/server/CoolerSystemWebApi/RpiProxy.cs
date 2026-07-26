using Microsoft.Extensions.Options;

namespace CoolerSystemWebApi;

/// <summary>
/// /api/{**path} を RPi 上の AEHAFmtSender の同名パスへそのまま中継する。
/// ボディ・クエリ・Content-Type は変換せず素通しし、レスポンスもそのまま返す
/// (JSON の形は AEHAFmtSender 側の NP081 / CirculatorConfig / AutomationConfig に一致する)。
/// 認証は Cloudflare Access が前段で行う前提のため、ここでは行わない。
/// </summary>
public static class RpiProxy
{
    public const string HttpClientName = "rpi";

    /// <summary>中継するプレフィクス。フロントの fetch はすべてこの下を叩く。</summary>
    private const string Prefix = "/api";

    private static readonly string[] Methods = ["GET", "POST"];

    public static IEndpointRouteBuilder MapRpiProxy(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods($"{Prefix}/{{**path}}", Methods, ForwardAsync);
        return endpoints;
    }

    private static async Task ForwardAsync(
        HttpContext ctx,
        string path,
        IHttpClientFactory factory,
        IOptions<RpiProxyOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(RpiProxy));
        var opts = options.Value;

        if (!opts.IsAllowed(ctx.Request.Method, path))
        {
            logger.LogWarning("中継対象外のパスです: {Method} {Path}", ctx.Request.Method, path);
            await WriteProblemAsync(ctx, StatusCodes.Status404NotFound, "中継対象外のパスです。");
            return;
        }

        var http = factory.CreateClient(HttpClientName);
        var target = new Uri(http.BaseAddress!, $"{path.TrimStart('/')}{ctx.Request.QueryString.Value}");

        using var request = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);

        // GET 以外はボディをそのまま流す (バッファせずストリームで渡す)。
        if (!HttpMethods.IsGet(ctx.Request.Method))
        {
            request.Content = new StreamContent(ctx.Request.Body);
            if (!string.IsNullOrEmpty(ctx.Request.ContentType))
                request.Content.Headers.TryAddWithoutValidation("Content-Type", ctx.Request.ContentType);
        }

        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            ctx.Response.StatusCode = (int)response.StatusCode;
            ctx.Response.Headers.CacheControl = "no-store";
            if (response.Content.Headers.ContentType is { } contentType)
                ctx.Response.ContentType = contentType.ToString();

            await response.Content.CopyToAsync(ctx.Response.Body, ct);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // クライアントが切断しただけ。レスポンスは書かない。
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "RPi への中継がタイムアウトしました: {Target}", target);
            await WriteProblemAsync(ctx, StatusCodes.Status504GatewayTimeout, "ラズパイからの応答がありません (タイムアウト)。");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "RPi への中継に失敗しました: {Target}", target);
            await WriteProblemAsync(ctx, StatusCodes.Status502BadGateway, "ラズパイに接続できません。");
        }
    }

    private static async Task WriteProblemAsync(HttpContext ctx, int statusCode, string detail)
    {
        if (ctx.Response.HasStarted)
            return;

        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsJsonAsync(new { status = statusCode, detail });
    }
}
