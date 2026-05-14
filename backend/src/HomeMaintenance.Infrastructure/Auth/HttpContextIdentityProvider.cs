using System.Security.Claims;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace HomeMaintenance.Infrastructure.Auth;

/// <summary>
/// Resolves the caller's <see cref="OwnerId"/> from the current
/// <see cref="HttpContext"/>'s authenticated principal. The OIDC
/// <c>sub</c> claim (or <see cref="ClaimTypes.NameIdentifier"/> if
/// the auth handler populated only that) becomes the OwnerId value.
/// </summary>
public sealed class HttpContextIdentityProvider : IIdentityProvider
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextIdentityProvider(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public OwnerId CurrentOwner
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            var sub = user?.FindFirstValue("sub")
                      ?? user?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? throw new InvalidOperationException(
                          "No authenticated principal available on the current HttpContext.");
            return new OwnerId(sub);
        }
    }
}
