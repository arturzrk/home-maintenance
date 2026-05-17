using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record AddStepCommand(string JobId, string Description);
public sealed record RemoveStepCommand(string JobId, string StepId);
public sealed record EditStepDescriptionCommand(string JobId, string StepId, string Description);
public sealed record ReorderStepsCommand(string JobId, IReadOnlyList<string> OrderedStepIds);

public abstract class StepMutationHandlerBase
{
    protected readonly IJobRepository Jobs;
    protected readonly IIdentityProvider Identity;
    protected readonly IAuditLog Audit;
    protected readonly ICorrelationContext Correlation;

    protected StepMutationHandlerBase(
        IJobRepository jobs,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        Jobs = jobs;
        Identity = identity;
        Audit = audit;
        Correlation = correlation;
    }

    protected async Task<(Job? job, Result<JobDetailDto>? failure)> LoadActive(
        string jobId,
        CancellationToken ct)
    {
        var job = await Jobs.GetAsync(jobId, Identity.CurrentOwner, ct);
        if (job is null)
            return (null, Result<JobDetailDto>.Failure(new NotFoundError("Job", jobId)));
        if (job.Status != JobStatus.Active)
            return (null, Result<JobDetailDto>.Failure(
                new BusinessRuleError("job_completed", "Job is completed; mutation not allowed.")));
        return (job, null);
    }

    protected Task RecordAudit(string eventType, Job job, string? target = null,
        IReadOnlyDictionary<string, object?>? payload = null,
        CancellationToken ct = default)
        => Audit.RecordAsync(new AuditEvent(
            eventType,
            job.Owner.Value,
            target ?? $"job:{job.Id}",
            DateTime.UtcNow,
            Correlation.CurrentId,
            payload), ct);
}

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
