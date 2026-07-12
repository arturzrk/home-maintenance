using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Assets;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace HomeMaintenance.Infrastructure.Persistence;

/// <summary>
/// MongoDB-backed <see cref="IAssetRepository"/>. Every query is
/// filtered by ownerId; cross-owner reads return null which the
/// handler layer translates to a 404 (no leak).
/// </summary>
internal sealed class AssetRepository : IAssetRepository
{
    internal const string CollectionName = "assets";

    private readonly IMongoCollection<AssetDocument> _collection;

    public AssetRepository(IMongoDatabase db)
    {
        _collection = db.GetCollection<AssetDocument>(CollectionName);
    }

    public async Task<Asset?> GetAsync(string id, OwnerId owner, CancellationToken ct = default)
    {
        var doc = await _collection
            .Find(d => d.Id == id && d.OwnerId == owner.Value)
            .FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<Asset>> ListByPropertyAsync(string propertyId, OwnerId owner, CancellationToken ct = default)
    {
        var docs = await _collection
            .Find(d => d.PropertyId == propertyId && d.OwnerId == owner.Value)
            .SortBy(d => d.Name)
            .ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public Task AddAsync(Asset asset, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var doc = new AssetDocument
        {
            Id = asset.Id,
            OwnerId = asset.Owner.Value,
            PropertyId = asset.PropertyId,
            Name = asset.Name,
            Category = asset.Category,
            Notes = asset.Notes,
            IsObsolete = asset.IsObsolete,
            CreatedAt = now,
            UpdatedAt = now,
        };
        return _collection.InsertOneAsync(doc, cancellationToken: ct);
    }

    public async Task UpdateAsync(Asset asset, CancellationToken ct = default)
    {
        var update = Builders<AssetDocument>.Update
            .Set(d => d.Name, asset.Name)
            .Set(d => d.Category, asset.Category)
            .Set(d => d.Notes, asset.Notes)
            .Set(d => d.IsObsolete, asset.IsObsolete)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(
            d => d.Id == asset.Id && d.OwnerId == asset.Owner.Value,
            update,
            cancellationToken: ct);
    }

    private static Asset ToDomain(AssetDocument doc)
        => Asset.Hydrate(
            doc.Id,
            new OwnerId(doc.OwnerId),
            doc.PropertyId,
            doc.Name,
            doc.Category,
            doc.Notes,
            doc.IsObsolete);
}
