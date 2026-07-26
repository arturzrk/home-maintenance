using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HomeMaintenance.Infrastructure.Persistence.Documents;

internal sealed class OwnerProfileDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("remindersEnabled")]
    public bool RemindersEnabled { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
