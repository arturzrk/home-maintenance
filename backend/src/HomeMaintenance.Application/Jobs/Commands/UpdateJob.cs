using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Commands;

/// <summary>
/// Partial-update command for a Job. Either or both of <c>Name</c> and
/// <c>DueDate</c> may be present; if neither is set the request is a
/// validation error. <c>DueDate</c> is a tri-state: not present (no
/// change), present with a value (set), or present with null (clear).
/// </summary>
public sealed record UpdateJobCommand(
    string JobId,
    string? Name,
    bool DueDateProvided,
    DateOnly? DueDate);

public sealed class UpdateJobHandler
{
    private readonly IJobRepository _jobs;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public UpdateJobHandler(
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

    public async Task<Result<JobDetailDto>> Handle(UpdateJobCommand cmd, CancellationToken ct = default)
    {
        if (IsEmptyRequest(cmd))
            return Result<JobDetailDto>.Failure(
                new ValidationError("request", "Provide at least one of name or dueDate."));

        var owner = _identity.CurrentOwner;
        var job = await _jobs.GetAsync(cmd.JobId, owner, ct);
        if (job is null)
            return Result<JobDetailDto>.Failure(new NotFoundError("Job", cmd.JobId));

        if (job.Status != JobStatus.Active)
            return Result<JobDetailDto>.Failure(
                new BusinessRuleError("job_completed", "Job is completed; mutation not allowed."));

        var audits = new AuditQueue();
        var error = ApplyRename(cmd, job, audits) ?? ApplyDueDate(cmd, job, audits);
        if (error is not null)
            return Result<JobDetailDto>.Failure(error);

        await _jobs.UpdateAsync(job, ct);

        foreach (var (eventType, payload) in audits)
            await EmitAudit(eventType, job.Id, owner, payload, ct);

        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }

    private static Error? ApplyRename(UpdateJobCommand cmd, Job job, AuditQueue audits)
    {
        if (cmd.Name is null)
            return null;

        try { job.Rename(cmd.Name); }
        catch (ArgumentException ex) { return new ValidationError("name", ex.Message); }

        audits.Add(AuditEventTypes.JobRenamed, new Dictionary<string, object?> { ["new_name"] = job.Name });
        return null;
    }

    private static Error? ApplyDueDate(UpdateJobCommand cmd, Job job, AuditQueue audits)
    {
        if (!cmd.DueDateProvided)
            return null;

        job.SetDueDate(cmd.DueDate);
        audits.Add(AuditEventTypes.JobDueDateChanged, new Dictionary<string, object?> { ["new_due_date"] = job.DueDate?.ToString("O") });
        return null;
    }

    private Task EmitAudit(string eventType, string jobId, OwnerId owner, Dictionary<string, object?> payload, CancellationToken ct)
        => _audit.RecordAsync(new AuditEvent(
            eventType,
            owner.Value,
            $"job:{jobId}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            payload), ct);

    private static bool IsEmptyRequest(UpdateJobCommand cmd)
        => cmd.Name is null && !cmd.DueDateProvided;

    /// <summary>Queues audit events to emit only after the whole update succeeds.</summary>
    private sealed class AuditQueue : List<(string EventType, Dictionary<string, object?> Payload)>
    {
        public void Add(string eventType, Dictionary<string, object?> payload) => Add((eventType, payload));
    }
}
