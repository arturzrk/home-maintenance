using HomeMaintenance.Infrastructure.AuditLog;
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
        await EnsurePropertyIndexes(cancellationToken);
        await EnsureAssetIndexes(cancellationToken);
        await EnsureJobIndexes(cancellationToken);
        await EnsureJobDefinitionIndexes(cancellationToken);
        await EnsureOwnerProfileIndexes(cancellationToken);
        await EnsureAuditLogIndexes(cancellationToken);
        _logger.LogInformation("MongoDB indexes ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private Task EnsurePropertyIndexes(CancellationToken ct)
    {
        var collection = _db.GetCollection<PropertyDocument>(PropertyRepository.CollectionName);
        return collection.Indexes.CreateManyAsync(
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
            ct);
    }

    private Task EnsureAssetIndexes(CancellationToken ct)
    {
        var collection = _db.GetCollection<AssetDocument>(AssetRepository.CollectionName);
        return collection.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<AssetDocument>(
                    Builders<AssetDocument>.IndexKeys.Ascending(d => d.OwnerId),
                    new CreateIndexOptions { Name = "owner_idx" }),
                new CreateIndexModel<AssetDocument>(
                    Builders<AssetDocument>.IndexKeys
                        .Ascending(d => d.OwnerId)
                        .Ascending(d => d.PropertyId)
                        .Ascending(d => d.Name),
                    new CreateIndexOptions { Name = "owner_property_name_idx" }),
            },
            ct);
    }

    private Task EnsureJobIndexes(CancellationToken ct)
    {
        var collection = _db.GetCollection<JobDocument>(JobRepository.CollectionName);
        return collection.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<JobDocument>(
                    Builders<JobDocument>.IndexKeys.Ascending(d => d.OwnerId),
                    new CreateIndexOptions { Name = "owner_idx" }),
                new CreateIndexModel<JobDocument>(
                    Builders<JobDocument>.IndexKeys
                        .Ascending(d => d.OwnerId)
                        .Ascending(d => d.PropertyId),
                    new CreateIndexOptions { Name = "owner_property_idx" }),
                new CreateIndexModel<JobDocument>(
                    Builders<JobDocument>.IndexKeys
                        .Ascending(d => d.OwnerId)
                        .Ascending(d => d.Status),
                    new CreateIndexOptions { Name = "owner_status_idx" }),
                new CreateIndexModel<JobDocument>(
                    Builders<JobDocument>.IndexKeys.Ascending(d => d.JobDefinitionId),
                    new CreateIndexOptions { Name = "job_definition_idx", Sparse = true }),
                new CreateIndexModel<JobDocument>(
                    Builders<JobDocument>.IndexKeys
                        .Ascending(d => d.Status)
                        .Ascending(d => d.DueDate),
                    new CreateIndexOptions { Name = "status_duedate_idx" }),
            },
            ct);
    }

    private Task EnsureJobDefinitionIndexes(CancellationToken ct)
    {
        var collection = _db.GetCollection<JobDefinitionDocument>(JobDefinitionRepository.CollectionName);
        return collection.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<JobDefinitionDocument>(
                    Builders<JobDefinitionDocument>.IndexKeys.Ascending(d => d.OwnerId),
                    new CreateIndexOptions { Name = "owner_idx" }),
                new CreateIndexModel<JobDefinitionDocument>(
                    Builders<JobDefinitionDocument>.IndexKeys
                        .Ascending(d => d.OwnerId)
                        .Ascending(d => d.PropertyId),
                    new CreateIndexOptions { Name = "owner_property_idx" }),
            },
            ct);
    }

    private Task EnsureOwnerProfileIndexes(CancellationToken ct)
    {
        var collection = _db.GetCollection<OwnerProfileDocument>(OwnerProfileRepository.CollectionName);
        return collection.Indexes.CreateOneAsync(
            new CreateIndexModel<OwnerProfileDocument>(
                Builders<OwnerProfileDocument>.IndexKeys.Ascending(d => d.OwnerId),
                new CreateIndexOptions { Name = "owner_idx", Unique = true }),
            cancellationToken: ct);
    }

    private Task EnsureAuditLogIndexes(CancellationToken ct)
    {
        var collection = _db.GetCollection<AuditLogDocument>(MongoAuditLog.CollectionName);

        // Partial filter for Target indexes: skip documents where Target is null
        // (auth events carry no target) so the index stays compact.
        var targetExists = Builders<AuditLogDocument>.Filter.Type(
            d => d.Target, MongoDB.Bson.BsonType.String);

        return collection.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<AuditLogDocument>(
                    Builders<AuditLogDocument>.IndexKeys
                        .Ascending(d => d.Actor)
                        .Descending(d => d.Timestamp),
                    new CreateIndexOptions { Name = "actor_timestamp_idx" }),

                new CreateIndexModel<AuditLogDocument>(
                    Builders<AuditLogDocument>.IndexKeys
                        .Ascending(d => d.Target)
                        .Descending(d => d.Timestamp),
                    new CreateIndexOptions<AuditLogDocument>
                    {
                        Name = "target_timestamp_idx",
                        PartialFilterExpression = targetExists,
                    }),

                new CreateIndexModel<AuditLogDocument>(
                    Builders<AuditLogDocument>.IndexKeys.Ascending(d => d.EventType),
                    new CreateIndexOptions { Name = "event_type_idx" }),

                new CreateIndexModel<AuditLogDocument>(
                    Builders<AuditLogDocument>.IndexKeys.Ascending(d => d.CorrelationId),
                    new CreateIndexOptions { Name = "correlation_id_idx" }),
            },
            ct);
    }
}
