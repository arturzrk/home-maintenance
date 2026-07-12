using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;
using HomeMaintenance.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace HomeMaintenance.Infrastructure.Persistence;

/// <summary>
/// MongoDB-backed <see cref="IJobDefinitionRepository"/>. User-facing queries
/// are scoped by ownerId; <see cref="ListAllActiveAsync"/> is the system-actor
/// query used by the background scheduler and intentionally has no owner filter.
/// </summary>
internal sealed class JobDefinitionRepository : IJobDefinitionRepository
{
    internal const string CollectionName = "job_definitions";

    private readonly IMongoCollection<JobDefinitionDocument> _collection;

    public JobDefinitionRepository(IMongoDatabase db)
    {
        _collection = db.GetCollection<JobDefinitionDocument>(CollectionName);
    }

    public async Task<JobDefinition?> GetAsync(string id, OwnerId owner, CancellationToken ct = default)
    {
        var doc = await _collection
            .Find(d => d.Id == id && d.OwnerId == owner.Value)
            .FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<JobDefinition>> ListAsync(OwnerId owner, string? propertyId, string? assetId = null, CancellationToken ct = default)
    {
        var builder = Builders<JobDefinitionDocument>.Filter;
        var filter = builder.Eq(d => d.OwnerId, owner.Value);
        if (!string.IsNullOrEmpty(propertyId))
            filter &= builder.Eq(d => d.PropertyId, propertyId);
        if (!string.IsNullOrEmpty(assetId))
            filter &= builder.Eq(d => d.AssetId, assetId);

        var docs = await _collection
            .Find(filter)
            .SortBy(d => d.Name)
            .ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<JobDefinition>> ListAllActiveAsync(CancellationToken ct = default)
    {
        var docs = await _collection
            .Find(FilterDefinition<JobDefinitionDocument>.Empty)
            .ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public Task AddAsync(JobDefinition definition, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var doc = ToDocument(definition);
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        return _collection.InsertOneAsync(doc, cancellationToken: ct);
    }

    public async Task UpdateAsync(JobDefinition definition, CancellationToken ct = default)
    {
        var existing = await _collection
            .Find(d => d.Id == definition.Id && d.OwnerId == definition.Owner.Value)
            .Project(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var doc = ToDocument(definition);
        doc.CreatedAt = existing;
        doc.UpdatedAt = DateTime.UtcNow;

        await _collection.ReplaceOneAsync(
            d => d.Id == definition.Id && d.OwnerId == definition.Owner.Value,
            doc,
            cancellationToken: ct);
    }

    // ---- Mappers ----

    private static JobDefinition ToDomain(JobDefinitionDocument doc)
    {
        var stepTemplates = doc.StepTemplates
            .OrderBy(s => s.Order)
            .Select(s => StepTemplate.Hydrate(s.Id, s.Order, s.Description));
        var schedule = new ScheduleDefinition(
            Enum.Parse<CadenceUnit>(doc.Schedule.Unit),
            doc.Schedule.Multiplier,
            doc.Schedule.StartDate,
            doc.Schedule.EndDate);
        return JobDefinition.Hydrate(doc.Id, new OwnerId(doc.OwnerId), doc.PropertyId, doc.Name, schedule, stepTemplates, doc.AssetId);
    }

    private static JobDefinitionDocument ToDocument(JobDefinition definition)
        => new()
        {
            Id = definition.Id,
            OwnerId = definition.Owner.Value,
            PropertyId = definition.PropertyId,
            AssetId = definition.AssetId,
            Name = definition.Name,
            Schedule = new ScheduleDefinitionDocument
            {
                Unit = definition.Schedule.Unit.ToString(),
                Multiplier = definition.Schedule.Multiplier,
                StartDate = definition.Schedule.StartDate,
                EndDate = definition.Schedule.EndDate,
            },
            StepTemplates = definition.StepTemplates.Select(st => new StepTemplateDocument
            {
                Id = st.Id,
                Order = st.Order,
                Description = st.Description,
            }).ToList(),
        };
}
