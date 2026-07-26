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
        string? assetId = null,
        CancellationToken ct = default);

    Task AddAsync(Job job, CancellationToken ct = default);

    Task UpdateAsync(Job job, CancellationToken ct = default);

    Task<bool> HasGeneratedJobForOccurrenceAsync(string definitionId, DateOnly dueDate, CancellationToken ct = default);

    Task<DateOnly?> LatestGeneratedJobDueDateAsync(string definitionId, CancellationToken ct = default);

    /// <summary>
    /// Every active job, across every owner, due on or before
    /// <paramref name="onOrBefore"/> (includes overdue jobs). System-wide -
    /// used only by the reminder digest scheduler; mirrors the owner-less
    /// <c>IJobDefinitionRepository.ListAllActiveAsync</c> precedent.
    /// </summary>
    Task<IReadOnlyList<Job>> ListDueOrOverdueAsync(DateOnly onOrBefore, CancellationToken ct = default);
}
