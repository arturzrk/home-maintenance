using HomeMaintenance.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HomeMaintenance.Infrastructure.AuditLog;

/// <summary>
/// Wires the <see cref="IAuditLog"/> implementation selected by
/// <c>AuditLog:Provider</c>:
/// <list type="bullet">
/// <item><description>
/// <c>"File"</c> — <see cref="FileAuditLog"/> (local dev / CI default).
/// Fails fast when the environment is <c>Production</c>.
/// </description></item>
/// <item><description>
/// <c>"Mongo"</c> — <see cref="MongoAuditLog"/> wrapped in
/// <see cref="AppInsightsAuditMirror"/> (staging + production).
/// The decorator catches insert failures, logs them at Error, and
/// does not rethrow so domain requests remain successful.
/// </description></item>
/// </list>
/// Any other value fails startup, mirroring the <c>Email:Provider</c>
/// pattern from WP02.
/// </summary>
public static class AuditLogExtensions
{
    public static IServiceCollection AddAuditLogging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment env)
    {
        services.Configure<AuditLogOptions>(
            configuration.GetSection(AuditLogOptions.SectionName));

        var provider = configuration["AuditLog:Provider"] ?? "File";

        if (string.Equals(provider, "File", StringComparison.OrdinalIgnoreCase))
        {
            if (env.IsProduction())
            {
                throw new InvalidOperationException(
                    "AuditLog:Provider 'File' is not permitted in the Production environment. " +
                    "The container filesystem is ephemeral; audit history would be lost on every " +
                    "restart or redeploy. Set AuditLog__Provider=Mongo (or another durable provider) " +
                    "in the Production app settings.");
            }

            services.AddSingleton<IAuditLog, FileAuditLog>();
        }
        else if (string.Equals(provider, "Mongo", StringComparison.OrdinalIgnoreCase))
        {
            // Register MongoAuditLog under a key so the decorator can
            // resolve it without a circular dependency on IAuditLog.
            services.AddKeyedSingleton<IAuditLog, MongoAuditLog>("mongo-inner",
                (sp, _) => new MongoAuditLog(sp.GetRequiredService<IMongoDatabase>()));

            // The outer IAuditLog is AppInsightsAuditMirror, which:
            //   1. Awaits the primary Mongo write.
            //   2. Catches failures, logs at Error, does NOT rethrow.
            //   3. On success, mirrors the envelope to App Insights via
            //      ActivitySource (true no-op when OTel is not configured).
            services.AddSingleton<IAuditLog>(sp => new AppInsightsAuditMirror(
                sp.GetRequiredKeyedService<IAuditLog>("mongo-inner"),
                sp.GetRequiredService<ILogger<AppInsightsAuditMirror>>()));
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown AuditLog:Provider '{provider}'. " +
                "Valid values are 'File' (local dev) or 'Mongo' (staging/production).");
        }

        return services;
    }
}
