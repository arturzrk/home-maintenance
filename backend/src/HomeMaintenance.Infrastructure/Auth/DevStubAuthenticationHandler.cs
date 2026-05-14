using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeMaintenance.Infrastructure.Auth;

/// <summary>
/// Local-development authentication handler. Accepts requests with
/// <c>Authorization: Bearer dev-&lt;sub&gt;</c> and resolves the suffix
/// after <c>dev-</c> to an authenticated principal whose <c>sub</c>
/// claim equals that suffix. ONLY ever wired up in the Development
/// environment; the startup assertion in
/// <see cref="AuthenticationExtensions"/> refuses to register this
/// handler when the environment is not Development.
/// </summary>
public sealed class DevStubAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevStub";
    private const string BearerPrefix = "Bearer dev-";

    public DevStubAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var auth))
            return Task.FromResult(AuthenticateResult.NoResult());

        var raw = auth.ToString();
        if (!raw.StartsWith(BearerPrefix, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());

        var sub = raw[BearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(sub))
            return Task.FromResult(AuthenticateResult.Fail("Empty dev-stub subject"));

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("sub", sub),
                new Claim(ClaimTypes.NameIdentifier, sub),
            },
            SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
