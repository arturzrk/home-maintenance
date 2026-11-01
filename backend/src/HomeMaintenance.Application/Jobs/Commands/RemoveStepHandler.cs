using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Commands;

public sealed class RemoveStepHandler : StepMutationHandlerBase
{
    public RemoveStepHandler(IJobRepository jobs, IIdentityProvider identity, IAuditLog audit, ICorrelationContext correlation)
        : base(jobs, identity, audit, correlation) { }

    public async Task<Result<JobDetailDto>> Handle(RemoveStepCommand cmd, CancellationToken ct = default)
    {
        var (job, failure) = await LoadActive(cmd.JobId, ct);
        if (failure is not null) return failure.Value;

        var outcome = job!.RemoveStep(cmd.StepId);
        if (outcome == StepMutationOutcome.StepNotFound)
            return Result<JobDetailDto>.Failure(new NotFoundError("Step", cmd.StepId));

        await Jobs.UpdateAsync(job, ct);
        await RecordAudit(
            AuditEventTypes.StepRemoved,
            job,
            target: $"job:{job.Id}/step:{cmd.StepId}",
            ct: ct);
        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}
