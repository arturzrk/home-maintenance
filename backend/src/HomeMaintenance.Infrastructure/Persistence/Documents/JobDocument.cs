using HomeMaintenance.Domain.Jobs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HomeMaintenance.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB persistence shape for the Job aggregate. Steps are embedded
/// (research.md R2): one round-trip per Job-level mutation, atomic
/// step updates inside the document.
/// </summary>
internal sealed class JobDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("propertyId")]
    public string PropertyId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("dueDate")]
    [BsonIgnoreIfNull]
    public DateOnly? DueDate { get; set; }

    [BsonElement("jobDefinitionId")]
    [BsonIgnoreIfNull]
    public string? JobDefinitionId { get; set; }

    [BsonElement("assetId")]
    [BsonIgnoreIfNull]
    public string? AssetId { get; set; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public JobStatus Status { get; set; } = JobStatus.Active;

    [BsonElement("completedAt")]
    [BsonIgnoreIfNull]
    public DateTime? CompletedAt { get; set; }

    [BsonElement("steps")]
    public List<StepDocument> Steps { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

internal sealed class StepDocument
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("order")]
    public int Order { get; set; }

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("isCompleted")]
    public bool IsCompleted { get; set; }

    [BsonElement("completedAt")]
    [BsonIgnoreIfNull]
    public DateTime? CompletedAt { get; set; }
}
