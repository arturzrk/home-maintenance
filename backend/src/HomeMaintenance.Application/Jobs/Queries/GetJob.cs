using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;

namespace HomeMaintenance.Application.Jobs.Queries;

public sealed record GetJobQuery(string Id);

public sealed class GetJobHandler
{
    private readonly IJobRepository _jobs;
    private readonly IIdentityProvider _identity;

    public GetJobHandler(IJobRepository jobs, IIdentityProvider identity)
    {
        _jobs = jobs;
        _identity = identity;
    }

    public async Task<Result<JobDetailDto>> Handle(GetJobQuery query, CancellationToken ct = default)
    {
        var job = await _jobs.GetAsync(query.Id, _identity.CurrentOwner, ct);
        return job is null
            ? Result<JobDetailDto>.Failure(new NotFoundError("Job", query.Id))
            : Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}
