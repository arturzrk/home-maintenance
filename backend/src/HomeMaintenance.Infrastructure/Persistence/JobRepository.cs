using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Jobs;
using HomeMaintenance.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace HomeMaintenance.Infrastructure.Persistence;

/// <summary>
/// MongoDB-backed <see cref="IJobRepository"/>. Every query is filtered
/// by ownerId (defence in depth + index alignment). The full document
/// is replaced on UpdateAsync; targeted `$set` / `$pull` optimisations
/// are a follow-up flagged in research.md R2.
/// </summary>
internal sealed class JobRepository : IJobRepository
{
    internal const string CollectionName = "jobs";

    private readonly IMongoCollection<JobDocument> _collection;

    public JobRepository(IMongoDatabase db)
    {
        _collection = db.GetCollection<JobDocument>(CollectionName);
    }

    public async Task<Job?> GetAsync(string id, OwnerId owner, CancellationToken ct = default)
    {
        var doc = await _collection
            .Find(d => d.Id == id && d.OwnerId == owner.Value)
            .FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<Job>> ListAsync(
        OwnerId owner,
        string? propertyId,
        JobStatus? status,
        CancellationToken ct = default)
    {
        var builder = Builders<JobDocument>.Filter;
        var filter = builder.Eq(d => d.OwnerId, owner.Value);
        if (!string.IsNullOrEmpty(propertyId))
            filter &= builder.Eq(d => d.PropertyId, propertyId);
        if (status.HasValue)
            filter &= builder.Eq(d => d.Status, status.Value);

        var docs = await _collection
            .Find(filter)
            .SortBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public Task AddAsync(Job job, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var doc = ToDocument(job);
        doc.CreatedAt = now;
        doc.UpdatedAt = now;
        return _collection.InsertOneAsync(doc, cancellationToken: ct);
    }

    public async Task UpdateAsync(Job job, CancellationToken ct = default)
    {
        var doc = ToDocument(job);
        doc.UpdatedAt = DateTime.UtcNow;
        // Replace the document, but keep CreatedAt untouched. Easiest path:
        // ReplaceOne uses the supplied doc as-is. We carry over CreatedAt by
        // looking it up first; for Slice 1's scale, the extra round-trip is
        // negligible. If it becomes hot, switch to $set.
        var existing = await _collection
            .Find(d => d.Id == job.Id && d.OwnerId == job.Owner.Value)
            .Project(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);
        doc.CreatedAt = existing;

        await _collection.ReplaceOneAsync(
            d => d.Id == job.Id && d.OwnerId == job.Owner.Value,
            doc,
            cancellationToken: ct);
    }

    // Implemented in WP03 once JobDefinitionId is persisted on the document.
    public Task<bool> HasGeneratedJobForOccurrenceAsync(string definitionId, DateOnly dueDate, CancellationToken ct = default)
        => throw new NotImplementedException("Implemented in WP03");

    public Task<DateOnly?> LatestGeneratedJobDueDateAsync(string definitionId, CancellationToken ct = default)
        => throw new NotImplementedException("Implemented in WP03");

    // ---- Mappers ----

    private static Job ToDomain(JobDocument doc)
    {
        var steps = doc.Steps
            .OrderBy(s => s.Order)
            .Select(s => Step.Hydrate(s.Id, s.Order, s.Description, s.IsCompleted, s.CompletedAt))
            .ToList();
        return Job.Hydrate(
            doc.Id,
            new OwnerId(doc.OwnerId),
            doc.PropertyId,
            doc.Name,
            doc.DueDate,
            doc.Status,
            doc.CompletedAt,
            steps);
    }

    private static JobDocument ToDocument(Job job)
        => new()
        {
            Id = job.Id,
            OwnerId = job.Owner.Value,
            PropertyId = job.PropertyId,
            Name = job.Name,
            DueDate = job.DueDate,
            Status = job.Status,
            CompletedAt = job.CompletedAt,
            Steps = job.Steps.Select(s => new StepDocument
            {
                Id = s.Id,
                Order = s.Order,
                Description = s.Description,
                IsCompleted = s.IsCompleted,
                CompletedAt = s.CompletedAt,
            }).ToList(),
        };
}
