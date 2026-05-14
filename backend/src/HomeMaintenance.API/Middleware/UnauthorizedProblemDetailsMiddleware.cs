namespace HomeMaintenance.API.Middleware;

/// <summary>
/// Surfaces ASP.NET Core's 401 challenges as RFC 7807 problem-details
/// JSON instead of an empty body. Runs after the authorization
/// middleware; if the response is 401 and nothing has been written
/// yet, writes a body with the same <c>code</c> + <c>correlationId</c>
/// shape used by handler-emitted failures.
///
/// This is the missing piece for default-deny: <c>FallbackPolicy</c>
/// triggers a 401 via <see cref="Microsoft.AspNetCore.Authorization.AuthorizationMiddleware"/>,
/// which by itself returns an empty response. Without this middleware
/// the 401 carries no machine-readable code.
/// </summary>
public sealed class UnauthorizedProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;

    public UnauthorizedProblemDetailsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        await _next(ctx);

        if (ctx.Response.StatusCode != StatusCodes.Status401Unauthorized) return;
        if (ctx.Response.HasStarted) return;
        if (ctx.Response.ContentLength is > 0) return;

        await ctx.Response.WriteAsJsonAsync(
            new
            {
                type = "https://home-maintenance/errors/unauthorized",
                title = "Unauthorized",
                status = StatusCodes.Status401Unauthorized,
                code = "unauthorized",
                detail = "Authentication required",
                correlationId = ctx.GetCorrelationId(),
            },
            options: null,
            contentType: "application/problem+json");
    }
}
