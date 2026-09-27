using HomeMaintenance.Application.Common.Interfaces;
using MongoDB.Driver;

namespace HomeMaintenance.Infrastructure.AuditLog;

/// <summary>
/// MongoDB-backed audit log. Writes one document per event to the
/// <c>auditEvents</c> collection with an explicit majority + journaled
/// write concern, so a successful return guarantees the event is durable
/// on a majority of Atlas replica-set members and is on disk.
///
/// This is the system-of-record sink. Failures propagate to the caller
/// (see <see cref="AppInsightsAuditMirror"/> for the failure-semantics
/// decorator that prevents audit errors from failing domain requests).
/// </summary>
public sealed class MongoAuditLog : IAuditLog
{
    /// <summary>Collection name in the application database.</summary>
    public const string CollectionName = "auditEvents";

    private readonly IMongoCollection<AuditLogDocument> _collection;

    public MongoAuditLog(IMongoDatabase database)
    {
        // Pin WMajority + journaling on this handle so the intent is
        // explicit in code, regardless of cluster or driver defaults.
        var writeConcern = new WriteConcern(mode: "majority", journal: true);

        _collection = database
            .GetCollection<AuditLogDocument>(CollectionName)
            .WithWriteConcern(writeConcern);
    }

    public Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
    {
        var doc = new AuditLogDocument
        {
            EventType    = evt.EventType,
            Actor        = evt.Actor,
            Target       = evt.Target,
            Timestamp    = evt.Timestamp,
            CorrelationId = evt.CorrelationId,
            Payload      = evt.Payload is null
                ? null
                : new Dictionary<string, object?>(evt.Payload),
        };

        return _collection.InsertOneAsync(doc, cancellationToken: ct);
    }
}
