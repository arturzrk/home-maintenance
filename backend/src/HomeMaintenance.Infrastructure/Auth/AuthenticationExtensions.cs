using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace HomeMaintenance.Infrastructure.Auth;

/// <summary>
/// Wires the application's authentication and authorization stack.
/// Two modes:
/// - <c>Auth:UseStub == true</c> (Development only): registers
///   <see cref="DevStubAuthenticationHandler"/> as the default scheme.
/// - Otherwise: registers JWT bearer validation against Google's OIDC
///   issuer with 24h JWKS caching and a 60s clock skew (see
///   research.md R3).
///
/// A fallback authorization policy requires every endpoint to be
/// authenticated unless explicitly marked <c>AllowAnonymous</c>.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddAppAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment env)
    {
        var useStub = configuration.GetValue<bool>("Auth:UseStub");

        if (useStub && !env.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Auth:UseStub MUST NOT be enabled outside the Development environment.");
        }

        var defaultScheme = useStub
            ? DevStubAuthenticationHandler.SchemeName
            : JwtBearerDefaults.AuthenticationScheme;

        var builder = services.AddAuthentication(defaultScheme);

        if (useStub)
        {
            builder.AddScheme<AuthenticationSchemeOptions, DevStubAuthenticationHandler>(
                DevStubAuthenticationHandler.SchemeName, _ => { });
        }
        else
        {
            var googleClientId = configuration["Auth:Google:ClientId"]
                                 ?? throw new InvalidOperationException(
                                     "Auth:Google:ClientId is required when Auth:UseStub is false");
            var googleAuthority = configuration["Auth:Google:Authority"]
                                  ?? "https://accounts.google.com";

            builder.AddJwtBearer(options =>
            {
                options.Authority = googleAuthority;
                options.Audience = googleClientId;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "https://accounts.google.com",
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(60),
                    NameClaimType = "sub",
                };
                options.RefreshOnIssuerKeyNotFound = true;
                options.AutomaticRefreshInterval = TimeSpan.FromHours(24);
            });
        }

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
