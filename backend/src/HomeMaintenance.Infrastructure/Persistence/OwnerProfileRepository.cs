using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace HomeMaintenance.Infrastructure.Persistence;

internal sealed class OwnerProfileRepository : IOwnerProfileRepository
{
    internal const string CollectionName = "owner-profiles";
    private readonly IMongoCollection<OwnerProfileDocument> _collection;

    public OwnerProfileRepository(IMongoDatabase db)
        => _collection = db.GetCollection<OwnerProfileDocument>(CollectionName);

    public async Task<OwnerProfile?> GetAsync(OwnerId owner, CancellationToken ct = default)
    {
        var doc = await _collection.Find(d => d.OwnerId == owner.Value).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task UpsertEmailAsync(OwnerId owner, string email, CancellationToken ct = default)
    {
        var existing = await _collection.Find(d => d.OwnerId == owner.Value).FirstOrDefaultAsync(ct);
        if (existing is not null && existing.Email == email)
            return;

        var now = DateTime.UtcNow;
        var filter = Builders<OwnerProfileDocument>.Filter.Eq(d => d.OwnerId, owner.Value);
        var update = Builders<OwnerProfileDocument>.Update
            .Set(d => d.Email, email)
            .Set(d => d.UpdatedAt, now)
            .SetOnInsert(d => d.Id, IdFactory.NewId())
            .SetOnInsert(d => d.RemindersEnabled, true)
            .SetOnInsert(d => d.CreatedAt, now);

        try
        {
            await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another authenticated request for the same owner (e.g. parallel
            // page-load calls) won the upsert race first - the profile now
            // exists, so a plain update finishes the job.
            await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        }
    }

    public async Task UpdateRemindersEnabledAsync(OwnerId owner, bool enabled, CancellationToken ct = default)
    {
        var update = Builders<OwnerProfileDocument>.Update
            .Set(d => d.RemindersEnabled, enabled)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(d => d.OwnerId == owner.Value, update, cancellationToken: ct);
    }

    private static OwnerProfile ToDomain(OwnerProfileDocument doc)
        => OwnerProfile.Hydrate(doc.Id, new OwnerId(doc.OwnerId), doc.Email, doc.RemindersEnabled);
}
