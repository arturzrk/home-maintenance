using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Commands;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Handlers;

public sealed class ReorderStepsHandler : StepMutationHandlerBase
{
    public ReorderStepsHandler(IJobRepository jobs, IIdentityProvider identity, IAuditLog audit, ICorrelationContext correlation)
        : base(jobs, identity, audit, correlation) { }

    public async Task<Result<JobDetailDto>> Handle(ReorderStepsCommand cmd, CancellationToken ct = default)
    {
        var (job, failure) = await LoadActive(cmd.JobId, ct);
        if (failure is not null) return failure.Value;

        var outcome = job!.ReorderSteps(cmd.OrderedStepIds);
        switch (outcome)
        {
            case ReorderStepsOutcome.WrongCount:
                return Result<JobDetailDto>.Failure(
                    new ValidationError("orderedStepIds", "Must list every existing step id exactly once."));
            case ReorderStepsOutcome.DuplicateId:
                return Result<JobDetailDto>.Failure(
                    new ValidationError("orderedStepIds", "Duplicate step id in the order list."));
            case ReorderStepsOutcome.UnknownId:
                return Result<JobDetailDto>.Failure(
                    new ValidationError("orderedStepIds", "Unknown step id in the order list."));
        }

        await Jobs.UpdateAsync(job, ct);
        await RecordAudit(
            AuditEventTypes.StepReordered,
            job,
            payload: new Dictionary<string, object?>
            {
                ["order"] = cmd.OrderedStepIds.ToList(),
            },
            ct: ct);
        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}
