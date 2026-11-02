using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Commands;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Handlers;

public sealed class UpdateJobHandler : StepMutationHandlerBase
{
    public UpdateJobHandler(
        IJobRepository jobs,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
        : base(jobs, identity, audit, correlation) { }

    public async Task<Result<JobDetailDto>> Handle(UpdateJobCommand cmd, CancellationToken ct = default)
    {
        if (IsEmptyRequest(cmd))
            return Result<JobDetailDto>.Failure(
                new ValidationError("request", "Provide at least one of name or dueDate."));

        var (job, failure) = await LoadActive(cmd.JobId, ct);
        if (failure is not null) return failure.Value;

        var audits = new AuditQueue();
        var error = ApplyRename(cmd, job!, audits) ?? ApplyDueDate(cmd, job!, audits);
        if (error is not null)
            return Result<JobDetailDto>.Failure(error);

        await Jobs.UpdateAsync(job!, ct);

        foreach (var (eventType, payload) in audits)
            await RecordAudit(eventType, job!, payload: payload, ct: ct);

        return Result<JobDetailDto>.Success(job!.ToDetailDto());
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

    private static bool IsEmptyRequest(UpdateJobCommand cmd)
        => cmd.Name is null && !cmd.DueDateProvided;

    /// <summary>Queues audit events to emit only after the whole update succeeds.</summary>
    private sealed class AuditQueue : List<(string EventType, Dictionary<string, object?> Payload)>
    {
        public void Add(string eventType, Dictionary<string, object?> payload) => Add((eventType, payload));
    }
}
