using System.Diagnostics;
using System.Reflection;
using HomeMaintenance.API.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HomeMaintenance.API.Endpoints;

/// <summary>
/// Standardized health endpoints. All are anonymous - they must stay
/// reachable by load balancers, Kubernetes probes, and the frontend
/// status widget without credentials, and must never expose secrets.
///
/// - /health    basic check (all registered checks); consumed by the
///              frontend widget, CI wait loops, and the Docker
///              HEALTHCHECK. 503 when a dependency is down.
/// - /liveness  process-is-responding only, no dependency checks; a
///              dependency outage must not make Kubernetes restart the
///              pod.
/// - /readiness checks tagged "ready" (MongoDB); 503 tells Kubernetes
///              to keep the pod out of rotation until dependencies
///              recover.
/// - /detailed  full operational view: status, version, uptime,
///              correlation id, and per-component latency.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteSummaryAsync,
        }).AllowAnonymous();

        app.MapHealthChecks("/liveness", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteSummaryAsync,
        }).AllowAnonymous();

        app.MapHealthChecks("/readiness", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = WriteSummaryAsync,
        }).AllowAnonymous();

        app.MapGet("/detailed", async (HealthCheckService healthChecks, HttpContext ctx, CancellationToken ct) =>
        {
            var report = await healthChecks.CheckHealthAsync(ct);

            var response = new
            {
                Status = ToStatusString(report.Status),
                Version = ApiVersion.Current,
                UptimeSeconds = (long)(DateTime.UtcNow - StartedAtUtc).TotalSeconds,
                CorrelationId = ctx.GetCorrelationId(),
                Checks = report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new
                    {
                        Status = ToStatusString(entry.Value.Status),
                        LatencyMs = (long)entry.Value.Duration.TotalMilliseconds,
                    }),
            };

            return report.Status == HealthStatus.Healthy
                ? Results.Ok(response)
                : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("DetailedHealth")
        .WithTags("System")
        .AllowAnonymous();
    }

    private static readonly DateTime StartedAtUtc = DateTime.UtcNow;

    private static Task WriteSummaryAsync(HttpContext ctx, HealthReport report)
        => ctx.Response.WriteAsJsonAsync(new
        {
            Status = ToStatusString(report.Status),
            Version = ApiVersion.Current,
        }, ctx.RequestAborted);

    // Never serialize check descriptions or exceptions: they can carry
    // connection details. Name, status, and latency only.
    private static string ToStatusString(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "healthy",
        HealthStatus.Degraded => "degraded",
        _ => "unhealthy",
    };
}

/// <summary>
/// Single source of the running API's version: the APP_VERSION
/// environment variable when set (deploy-time override), otherwise the
/// assembly's informational version (the csproj &lt;Version&gt;
/// property). The "+commitsha" suffix .NET appends when source-link
/// metadata is present is trimmed.
/// </summary>
public static class ApiVersion
{
    public static readonly string Current = Resolve();

    private static string Resolve()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("APP_VERSION");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment.Trim();

        var informational = typeof(ApiVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
            return "unknown";

        var plusIndex = informational.IndexOf('+');
        return plusIndex < 0 ? informational : informational[..plusIndex];
    }
}
