using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;
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
        if (cmd.Name is null && !cmd.DueDateProvided)
            return Result<JobDetailDto>.Failure(
                new ValidationError("request", "Provide at least one of name or dueDate."));

        var owner = _identity.CurrentOwner;
        var job = await _jobs.GetAsync(cmd.JobId, owner, ct);
        if (job is null)
            return Result<JobDetailDto>.Failure(new NotFoundError("Job", cmd.JobId));

        if (job.Status != JobStatus.Active)
            return Result<JobDetailDto>.Failure(
                new BusinessRuleError("job_completed", "Job is completed; mutation not allowed."));

        var changes = new Dictionary<string, object?>();
        if (cmd.Name is not null)
        {
            var oldName = job.Name;
            try
            {
                job.Rename(cmd.Name);
            }
            catch (ArgumentException ex)
            {
                return Result<JobDetailDto>.Failure(new ValidationError("name", ex.Message));
            }
            changes["name"] = new { old_value = oldName, new_value = job.Name };
        }

        if (cmd.DueDateProvided)
        {
            var oldDueDate = job.DueDate;
            job.SetDueDate(cmd.DueDate);
            changes["dueDate"] = new { old_value = oldDueDate?.ToString("O"), new_value = job.DueDate?.ToString("O") };
        }

        await _jobs.UpdateAsync(job, ct);

        if (cmd.Name is not null)
        {
            await _audit.RecordAsync(new AuditEvent(
                AuditEventTypes.JobRenamed,
                owner.Value,
                $"job:{job.Id}",
                DateTime.UtcNow,
                _correlation.CurrentId,
                new Dictionary<string, object?> { ["new_name"] = job.Name }), ct);
        }
        if (cmd.DueDateProvided)
        {
            await _audit.RecordAsync(new AuditEvent(
                AuditEventTypes.JobDueDateChanged,
                owner.Value,
                $"job:{job.Id}",
                DateTime.UtcNow,
                _correlation.CurrentId,
                new Dictionary<string, object?> { ["new_due_date"] = job.DueDate?.ToString("O") }), ct);
        }

        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}
