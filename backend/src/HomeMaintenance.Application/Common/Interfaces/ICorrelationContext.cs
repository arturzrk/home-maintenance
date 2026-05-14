namespace HomeMaintenance.Application.Common.Interfaces;

/// <summary>
/// Exposes the request-scoped correlation id to use-case handlers
/// without leaking <see cref="Microsoft.AspNetCore.Http.HttpContext"/>
/// into the Application layer. Concrete implementations read from
/// HttpContext.Items (production) or return a stub value (tests).
/// </summary>
public interface ICorrelationContext
{
    string CurrentId { get; }
}
