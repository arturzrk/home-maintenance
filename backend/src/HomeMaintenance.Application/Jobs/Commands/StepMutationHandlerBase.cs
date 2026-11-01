using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Commands;

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
