using HomeMaintenance.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HomeMaintenance.Infrastructure.Auth;

/// <summary>
/// Reads the correlation id stamped onto the request by
/// <c>CorrelationIdMiddleware</c>. Falls back to an empty string when
/// no HttpContext is in scope (e.g. background work outside a request,
/// which doesn't occur in Slice 1 but is documented).
/// </summary>
public sealed class HttpContextCorrelationContext : ICorrelationContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCorrelationContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string CurrentId
        => _accessor.HttpContext?.Items["CorrelationId"] as string ?? string.Empty;
}
