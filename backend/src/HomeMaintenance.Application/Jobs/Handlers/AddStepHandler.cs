using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Commands;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Handlers;

public sealed class AddStepHandler : StepMutationHandlerBase
{
    public AddStepHandler(IJobRepository jobs, IIdentityProvider identity, IAuditLog audit, ICorrelationContext correlation)
        : base(jobs, identity, audit, correlation) { }

    public async Task<Result<JobDetailDto>> Handle(AddStepCommand cmd, CancellationToken ct = default)
    {
        var (job, failure) = await LoadActive(cmd.JobId, ct);
        if (failure is not null) return failure.Value;

        Step step;
        try
        {
            step = job!.AddStep(cmd.Description);
        }
        catch (ArgumentException ex)
        {
            return Result<JobDetailDto>.Failure(new ValidationError("description", ex.Message));
        }

        await Jobs.UpdateAsync(job!, ct);
        await RecordAudit(
            AuditEventTypes.StepAdded,
            job!,
            target: $"job:{job.Id}/step:{step.Id}",
            ct: ct);
        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}
