using System.Security.Claims;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.API.Middleware;

/// <summary>
/// Runs after authentication on every request. Reads the <c>email</c>
/// claim already present on the validated token and upserts it onto the
/// owner's profile - the only way an owner's contact email is ever
/// captured (no separate frontend endpoint exists). No email claim on
/// the request simply means no upsert; that owner won't receive
/// reminder digests until a future request carries one.
/// </summary>
public sealed class OwnerProfileSyncMiddleware
{
    private readonly RequestDelegate _next;

    public OwnerProfileSyncMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IOwnerProfileRepository profiles)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email);

            if (!string.IsNullOrWhiteSpace(sub) && !string.IsNullOrWhiteSpace(email))
            {
                await profiles.UpsertEmailAsync(new OwnerId(sub), email, context.RequestAborted);
            }
        }

        await _next(context);
    }
}
