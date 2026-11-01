using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Commands;

public sealed class EditStepDescriptionHandler : StepMutationHandlerBase
{
    public EditStepDescriptionHandler(IJobRepository jobs, IIdentityProvider identity, IAuditLog audit, ICorrelationContext correlation)
        : base(jobs, identity, audit, correlation) { }

    public async Task<Result<JobDetailDto>> Handle(EditStepDescriptionCommand cmd, CancellationToken ct = default)
    {
        var (job, failure) = await LoadActive(cmd.JobId, ct);
        if (failure is not null) return failure.Value;

        StepMutationOutcome outcome;
        try
        {
            outcome = job!.EditStepDescription(cmd.StepId, cmd.Description);
        }
        catch (ArgumentException ex)
        {
            return Result<JobDetailDto>.Failure(new ValidationError("description", ex.Message));
        }

        if (outcome == StepMutationOutcome.StepNotFound)
            return Result<JobDetailDto>.Failure(new NotFoundError("Step", cmd.StepId));

        await Jobs.UpdateAsync(job, ct);
        await RecordAudit(
            AuditEventTypes.StepDescriptionEdited,
            job,
            target: $"job:{job.Id}/step:{cmd.StepId}",
            ct: ct);
        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}
