using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for the Job aggregate. Every query is scoped
/// to a single <see cref="OwnerId"/>; cross-owner reads return null,
/// which handlers map to NotFoundError.
/// </summary>
public interface IJobRepository
{
    Task<Job?> GetAsync(string id, OwnerId owner, CancellationToken ct = default);

    Task<IReadOnlyList<Job>> ListAsync(
        OwnerId owner,
        string? propertyId,
        JobStatus? status,
        CancellationToken ct = default);

    Task AddAsync(Job job, CancellationToken ct = default);

    Task UpdateAsync(Job job, CancellationToken ct = default);
}
