namespace HomeMaintenance.API.Middleware;

/// <summary>
/// Stamps every request with a stable correlation id. The id is read
/// from the inbound <c>X-Correlation-Id</c> header if present, or
/// generated as a GUID-N otherwise. The same id is echoed back as a
/// response header, surfaced to handlers via <c>HttpContext.Items</c>,
/// and pushed onto the logging scope so every log line emitted during
/// the request carries it.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, ILogger<CorrelationIdMiddleware> logger)
    {
        var id = ctx.Request.Headers.TryGetValue(HeaderName, out var existing)
                 && !string.IsNullOrWhiteSpace(existing.ToString())
            ? existing.ToString()
            : Guid.NewGuid().ToString("N");

        ctx.Items[ItemKey] = id;
        ctx.Response.Headers[HeaderName] = id;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = id,
        }))
        {
            await _next(ctx);
        }
    }
}

public static class CorrelationIdExtensions
{
    public static string GetCorrelationId(this HttpContext ctx)
        => ctx.Items["CorrelationId"] as string ?? string.Empty;
}
