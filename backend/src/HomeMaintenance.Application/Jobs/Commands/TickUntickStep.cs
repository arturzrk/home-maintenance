using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record TickStepCommand(string JobId, string StepId);
public sealed record UntickStepCommand(string JobId, string StepId);

public sealed class TickStepHandler
{
    private readonly IJobRepository _jobs;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public TickStepHandler(
        IJobRepository jobs,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _jobs = jobs;
        _identity = identity;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<JobDetailDto>> Handle(TickStepCommand cmd, CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;
        var job = await _jobs.GetAsync(cmd.JobId, owner, ct);
        if (job is null)
            return Result<JobDetailDto>.Failure(new NotFoundError("Job", cmd.JobId));

        if (job.Status != JobStatus.Active)
            return Result<JobDetailDto>.Failure(
                new BusinessRuleError("job_completed", "Job is completed; mutation not allowed."));

        var outcome = job.TickStep(cmd.StepId, DateTime.UtcNow);
        if (outcome == StepMutationOutcome.StepNotFound)
            return Result<JobDetailDto>.Failure(new NotFoundError("Step", cmd.StepId));

        await _jobs.UpdateAsync(job, ct);
        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.StepTicked,
            owner.Value,
            $"job:{job.Id}/step:{cmd.StepId}",
            DateTime.UtcNow,
            _correlation.CurrentId), ct);

        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}

public sealed class UntickStepHandler
{
    private readonly IJobRepository _jobs;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public UntickStepHandler(
        IJobRepository jobs,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _jobs = jobs;
        _identity = identity;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<JobDetailDto>> Handle(UntickStepCommand cmd, CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;
        var job = await _jobs.GetAsync(cmd.JobId, owner, ct);
        if (job is null)
            return Result<JobDetailDto>.Failure(new NotFoundError("Job", cmd.JobId));

        if (job.Status != JobStatus.Active)
            return Result<JobDetailDto>.Failure(
                new BusinessRuleError("job_completed", "Job is completed; mutation not allowed."));

        var outcome = job.UntickStep(cmd.StepId);
        if (outcome == StepMutationOutcome.StepNotFound)
            return Result<JobDetailDto>.Failure(new NotFoundError("Step", cmd.StepId));

        await _jobs.UpdateAsync(job, ct);
        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.StepUnticked,
            owner.Value,
            $"job:{job.Id}/step:{cmd.StepId}",
            DateTime.UtcNow,
            _correlation.CurrentId), ct);

        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}
