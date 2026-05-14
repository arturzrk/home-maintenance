using HomeMaintenance.Infrastructure.Persistence.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HomeMaintenance.Infrastructure.Persistence;

/// <summary>
/// Ensures the required indexes exist on first startup. Idempotent:
/// MongoDB's <c>CreateOneAsync</c> is a no-op when an equivalent index
/// is already present. Runs once per host start.
/// </summary>
internal sealed class MongoIndexInitializer : IHostedService
{
    private readonly IMongoDatabase _db;
    private readonly ILogger<MongoIndexInitializer> _logger;

    public MongoIndexInitializer(IMongoDatabase db, ILogger<MongoIndexInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var properties = _db.GetCollection<PropertyDocument>(PropertyRepository.CollectionName);

        await properties.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<PropertyDocument>(
                    Builders<PropertyDocument>.IndexKeys.Ascending(d => d.OwnerId),
                    new CreateIndexOptions { Name = "owner_idx" }),
                new CreateIndexModel<PropertyDocument>(
                    Builders<PropertyDocument>.IndexKeys
                        .Ascending(d => d.OwnerId)
                        .Ascending(d => d.Name),
                    new CreateIndexOptions { Name = "owner_name_idx" }),
            },
            cancellationToken);

        _logger.LogInformation("MongoDB indexes ensured for collection {Collection}", PropertyRepository.CollectionName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
