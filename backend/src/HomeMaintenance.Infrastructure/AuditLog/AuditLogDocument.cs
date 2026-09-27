using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HomeMaintenance.Infrastructure.AuditLog;

/// <summary>
/// MongoDB document shape for an audit event stored in the
/// <c>auditEvents</c> collection. Mirrors <see cref="Application.Common.Interfaces.AuditEvent"/>
/// unchanged so the full event is retained as the system of record.
/// </summary>
public sealed class AuditLogDocument
{
    [BsonId]
    public ObjectId Id { get; init; }

    public string EventType { get; init; } = string.Empty;
    public string Actor { get; init; } = string.Empty;
    public string? Target { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Arbitrary payload carried from the domain command.
    /// Stored in full on the Mongo system-of-record document;
    /// never forwarded to the App Insights mirror.
    /// </summary>
    public Dictionary<string, object?>? Payload { get; init; }
}
