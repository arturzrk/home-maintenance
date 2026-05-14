namespace HomeMaintenance.Application.Common;

/// <summary>
/// Base error type returned via <see cref="Result{T}"/> when an operation
/// fails for a known, expected reason. Concrete variants exist for each
/// failure shape the API surface translates to an HTTP response.
/// </summary>
public abstract record Error(string Code, string Message);

public sealed record NotFoundError(string ResourceType, string Id)
    : Error("not_found", $"{ResourceType} {Id} not found");

public sealed record ValidationError(string Field, string Reason)
    : Error("validation", $"{Field}: {Reason}");

public sealed record BusinessRuleError(string Rule, string Message)
    : Error("business_rule", Message);

public sealed record UnauthorizedError()
    : Error("unauthorized", "Authentication required");

public sealed record ForbiddenError()
    : Error("forbidden", "Caller does not own this resource");
