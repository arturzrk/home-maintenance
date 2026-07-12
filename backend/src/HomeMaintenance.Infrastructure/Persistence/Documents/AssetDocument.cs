using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HomeMaintenance.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB persistence shape for the Asset aggregate. Stays inside
/// Infrastructure; never serialised to the wire (constitution rule).
/// </summary>
internal sealed class AssetDocument
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

    [BsonElement("category")]
    [BsonIgnoreIfNull]
    public string? Category { get; set; }

    [BsonElement("notes")]
    [BsonIgnoreIfNull]
    public string? Notes { get; set; }

    [BsonElement("isObsolete")]
    public bool IsObsolete { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
