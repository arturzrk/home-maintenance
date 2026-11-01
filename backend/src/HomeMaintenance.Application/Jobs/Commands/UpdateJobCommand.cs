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
