using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HomeMaintenance.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB persistence shape for the JobDefinition aggregate. Step templates
/// and the schedule are embedded, mirroring the JobDocument pattern.
/// </summary>
internal sealed class JobDefinitionDocument
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

    [BsonElement("schedule")]
    public ScheduleDefinitionDocument Schedule { get; set; } = null!;

    [BsonElement("stepTemplates")]
    public List<StepTemplateDocument> StepTemplates { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

internal sealed class StepTemplateDocument
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("order")]
    public int Order { get; set; }

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;
}

internal sealed class ScheduleDefinitionDocument
{
    [BsonElement("unit")]
    public string Unit { get; set; } = string.Empty;

    [BsonElement("multiplier")]
    public int Multiplier { get; set; }

    [BsonElement("startDate")]
    public DateOnly StartDate { get; set; }

    [BsonElement("endDate")]
    [BsonIgnoreIfNull]
    public DateOnly? EndDate { get; set; }
}
