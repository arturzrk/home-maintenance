using MongoDB.Driver;

namespace HomeMaintenance.Infrastructure.Persistence;

/// <summary>
/// Wraps the MongoDB <see cref="IMongoDatabase"/> and exposes typed collection accessors.
/// All repository implementations receive this via DI.
/// </summary>
public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Returns a strongly typed MongoDB collection by name.
    /// Repositories call this to obtain their collection reference.
    /// </summary>
    public IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName)
        => _database.GetCollection<TDocument>(collectionName);
}
