namespace HomeMaintenance.Application.Common.Interfaces;

/// <summary>
/// Append-only audit log. Concrete sinks live in Infrastructure
/// (local file for Slice 1; managed sink such as Azure Monitor or
/// Splunk for any non-local deployment - see constitution Phase 2).
/// </summary>
public interface IAuditLog
{
    Task RecordAsync(AuditEvent evt, CancellationToken ct = default);
}

/// <summary>
/// One audit log entry. Persisted as one JSON object per line by
/// FileAuditLog; the shape carries over to managed sinks unchanged.
/// </summary>
public sealed record AuditEvent(
    string EventType,
    string Actor,
    string? Target,
    DateTime Timestamp,
    string CorrelationId,
    IReadOnlyDictionary<string, object?>? Payload = null);

/// <summary>
/// Canonical event-type strings. Enumerated here so producers and
/// consumers share a single source of truth. Mirrors the table in
/// polaris-specs/001-property-job-step/spec.md "Audit Logging".
/// </summary>
public static class AuditEventTypes
{
    public const string AuthSigninSuccess = "auth.signin_success";
    public const string AuthSigninFailure = "auth.signin_failure";
    public const string AuthzDenied = "authz.denied";

    public const string PropertyCreated = "property.created";
    public const string PropertyRenamed = "property.renamed";

    public const string JobCreated = "job.created";
    public const string JobRenamed = "job.renamed";
    public const string JobDueDateChanged = "job.due_date_changed";
    public const string JobCompleted = "job.completed";

    public const string StepAdded = "step.added";
    public const string StepRemoved = "step.removed";
    public const string StepReordered = "step.reordered";
    public const string StepDescriptionEdited = "step.description_edited";
    public const string StepTicked = "step.ticked";
    public const string StepUnticked = "step.unticked";
}
