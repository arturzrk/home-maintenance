using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Properties;
using HomeMaintenance.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace HomeMaintenance.Infrastructure.Persistence;

/// <summary>
/// MongoDB-backed <see cref="IPropertyRepository"/>. Every query is
/// filtered by ownerId; cross-owner reads return null which the
/// handler layer translates to a 404 (no leak).
/// </summary>
internal sealed class PropertyRepository : IPropertyRepository
{
    internal const string CollectionName = "properties";

    private readonly IMongoCollection<PropertyDocument> _collection;

    public PropertyRepository(IMongoDatabase db)
    {
        _collection = db.GetCollection<PropertyDocument>(CollectionName);
    }

    public async Task<Property?> GetAsync(string id, OwnerId owner, CancellationToken ct = default)
    {
        var doc = await _collection
            .Find(d => d.Id == id && d.OwnerId == owner.Value)
            .FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<Property>> ListAsync(OwnerId owner, CancellationToken ct = default)
    {
        var docs = await _collection
            .Find(d => d.OwnerId == owner.Value)
            .SortBy(d => d.Name)
            .ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public Task AddAsync(Property property, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var doc = new PropertyDocument
        {
            Id = property.Id,
            OwnerId = property.Owner.Value,
            Name = property.Name,
            CreatedAt = now,
            UpdatedAt = now,
        };
        return _collection.InsertOneAsync(doc, cancellationToken: ct);
    }

    public async Task UpdateAsync(Property property, CancellationToken ct = default)
    {
        var update = Builders<PropertyDocument>.Update
            .Set(d => d.Name, property.Name)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(
            d => d.Id == property.Id && d.OwnerId == property.Owner.Value,
            update,
            cancellationToken: ct);
    }

    private static Property ToDomain(PropertyDocument doc)
        => Property.Hydrate(doc.Id, new OwnerId(doc.OwnerId), doc.Name);
}
