using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record CompleteJobCommand(string JobId);

public sealed class CompleteJobHandler
{
    private readonly IJobRepository _jobs;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public CompleteJobHandler(
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

    public async Task<Result<JobDetailDto>> Handle(CompleteJobCommand cmd, CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;
        var job = await _jobs.GetAsync(cmd.JobId, owner, ct);
        if (job is null)
            return Result<JobDetailDto>.Failure(new NotFoundError("Job", cmd.JobId));

        var outcome = job.Complete(DateTime.UtcNow);
        switch (outcome)
        {
            case CompleteJobOutcome.AlreadyCompleted:
                return Result<JobDetailDto>.Failure(
                    new BusinessRuleError("job_already_completed", "Job is already completed."));
            case CompleteJobOutcome.NoSteps:
                return Result<JobDetailDto>.Failure(
                    new BusinessRuleError("job_has_no_steps", "Job has no steps; nothing to complete."));
            case CompleteJobOutcome.StepsIncomplete:
                return Result<JobDetailDto>.Failure(
                    new BusinessRuleError("steps_incomplete", "Not all steps are completed."));
        }

        await _jobs.UpdateAsync(job, ct);
        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.JobCompleted,
            owner.Value,
            $"job:{job.Id}",
            DateTime.UtcNow,
            _correlation.CurrentId), ct);

        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}
