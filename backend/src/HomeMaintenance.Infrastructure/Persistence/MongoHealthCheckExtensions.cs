using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace HomeMaintenance.Infrastructure.Persistence;

/// <summary>
/// Wires the MongoDB health check, resolving <see cref="IMongoDatabase"/>
/// from DI (registered by <see cref="DependencyInjection.AddInfrastructure"/>)
/// so the API project never needs a direct dependency on MongoDB.Driver.
/// </summary>
public static class MongoHealthCheckExtensions
{
    public static IHealthChecksBuilder AddMongoHealthCheck(this IHealthChecksBuilder builder)
        => builder.AddMongoDb(
            sp => sp.GetRequiredService<IMongoDatabase>(),
            name: "mongodb",
            tags: ["db", "ready"]);
}
