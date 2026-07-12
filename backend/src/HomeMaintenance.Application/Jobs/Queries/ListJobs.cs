using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Queries;

public sealed record ListJobsQuery(string? PropertyId, JobStatus? Status, string? AssetId = null);

public sealed class ListJobsHandler
{
    private readonly IJobRepository _jobs;
    private readonly IIdentityProvider _identity;

    public ListJobsHandler(IJobRepository jobs, IIdentityProvider identity)
    {
        _jobs = jobs;
        _identity = identity;
    }

    public async Task<Result<JobListDto>> Handle(ListJobsQuery query, CancellationToken ct = default)
    {
        var jobs = await _jobs.ListAsync(
            _identity.CurrentOwner,
            query.PropertyId,
            query.Status,
            query.AssetId,
            ct);

        return Result<JobListDto>.Success(
            new JobListDto(jobs.Select(j => j.ToSummaryDto()).ToList()));
    }
}
