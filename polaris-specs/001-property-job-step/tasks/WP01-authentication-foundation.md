---
work_package_id: WP01
lane: done
dependencies: []
base_branch: main
base_commit: 50b56c07e3510a40f5d3327f0f815d736566f6ac
created_at: '2026-05-14T14:18:31.414951+00:00'
subtasks: [T001, T002, T003, T004, T005, T006, T007]
shell_pid: "62492"
test_status: required
test_file: tests/e2e/WP01-wp01-authentication-foundation.e2e.js
domain: backend-logic
agent: "claude"
---

# WP01 - Authentication foundation

## Objective

Stand up the cross-cutting auth and result primitives every later WP
depends on: typed `Result<T>`, named `Error` records, the `OwnerId`
value object, the `IIdentityProvider` abstraction (and its
HttpContext-backed implementation), Google OIDC JWT validation, a
header-based local stub, and a startup assertion that refuses to run
the stub in production. No business endpoints exist yet; this WP only
adds plumbing.

## Inputs

- Spec: `polaris-specs/001-property-job-step/spec.md` (FR-001..FR-008,
  Audit/Security section).
- Plan: `polaris-specs/001-property-job-step/plan.md` (Constitution
  Check row "AuthN/AuthZ"; section "Implementation Strategy" WP01).
- Research: `polaris-specs/001-property-job-step/research.md` R3
  (JWKS cache), R4 (local stub), R5 (Result + Error shape).
- Data model: `data-model.md` "OwnerId" + "Application layer / IIdentityProvider".

## Subtasks

### T001 [P] - Result<T> and typed Error records

Create:

- `backend/src/HomeMaintenance.Application/Common/Result.cs`
- `backend/src/HomeMaintenance.Application/Common/Errors.cs`
- `backend/src/HomeMaintenance.Application/Common/None.cs`
  (placeholder type for `Result<None>` when no value is returned).

Shape lifted verbatim from `research.md` R5: `Result<T>` is a readonly
record struct with `Value`, `Error`, `IsSuccess`, plus `Success(T)` and
`Failure(Error)` factories. `Error` is an `abstract record`; concrete
variants: `NotFoundError(string ResourceType, string Id)`,
`ValidationError(string Field, string Reason)`,
`BusinessRuleError(string Rule, string Message)`,
`UnauthorizedError()`, `ForbiddenError()`.

Unit tests in
`backend/tests/HomeMaintenance.Unit.Tests/Application/Common/`:
- `Result_Success_HoldsValue`
- `Result_Failure_HoldsError_NotValue`
- `Result_IsSuccess_TrueOnlyWhenErrorNull`
- one test per `Error` variant verifying `Code` and `Message`

### T002 [P] - OwnerId value object

Create `backend/src/HomeMaintenance.Domain/Identity/OwnerId.cs`.

Shape from `data-model.md`. Sealed record with a single `Value`
property, throws `ArgumentException` on null/whitespace, implicit
string conversion, ToString override.

Unit tests in
`backend/tests/HomeMaintenance.Unit.Tests/Domain/Identity/`:
- Construction with valid string succeeds.
- Construction with `null`, `""`, `"   "` throws.
- Two instances with the same `Value` are equal.
- Two instances with different `Value` are not equal.
- ToString returns `Value`.

### T003 - IIdentityProvider abstraction

Create
`backend/src/HomeMaintenance.Application/Common/Interfaces/IIdentityProvider.cs`:

```csharp
public interface IIdentityProvider
{
    OwnerId CurrentOwner { get; }
}
```

Create
`backend/src/HomeMaintenance.Infrastructure/Auth/HttpContextIdentityProvider.cs`:

```csharp
public sealed class HttpContextIdentityProvider : IIdentityProvider
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextIdentityProvider(IHttpContextAccessor accessor)
        => _accessor = accessor;

    public OwnerId CurrentOwner
    {
        get
        {
            var sub = _accessor.HttpContext?.User?
                .FindFirstValue(ClaimTypes.NameIdentifier)
                ?? _accessor.HttpContext?.User?.FindFirstValue("sub")
                ?? throw new InvalidOperationException(
                    "No authenticated principal available.");
            return new OwnerId(sub);
        }
    }
}
```

Register in `Infrastructure.DependencyInjection.AddInfrastructure`:
```csharp
services.AddHttpContextAccessor();
services.AddScoped<IIdentityProvider, HttpContextIdentityProvider>();
```

The Application layer references the interface only; the Infrastructure
implementation is the sole binding.

### T004 - Google OIDC JWT bearer validation

Extend `backend/src/HomeMaintenance.API/Program.cs` (or extract to
`HomeMaintenance.Infrastructure.Auth.AuthenticationExtensions`):

```csharp
public static IServiceCollection AddAppAuthentication(
    this IServiceCollection services,
    IConfiguration configuration,
    IWebHostEnvironment env)
{
    var useStub = configuration.GetValue<bool>("Auth:UseStub");

    // R4: startup assertion. Stub is dev-only.
    if (useStub && !env.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Auth:UseStub MUST NOT be enabled outside the Development environment.");
    }

    var builder = services.AddAuthentication(useStub
        ? "DevStub"
        : JwtBearerDefaults.AuthenticationScheme);

    if (useStub)
    {
        builder.AddScheme<AuthenticationSchemeOptions, DevStubAuthenticationHandler>(
            "DevStub", _ => { });
    }
    else
    {
        builder.AddJwtBearer(options =>
        {
            options.Authority = configuration["Auth:Google:Authority"]
                ?? "https://accounts.google.com";
            options.Audience = configuration["Auth:Google:ClientId"]
                ?? throw new InvalidOperationException(
                    "Auth:Google:ClientId is required when Auth:UseStub is false");
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
```

Update `Program.cs` to call `builder.Services.AddAppAuthentication(...)`
and to add `app.UseAuthentication(); app.UseAuthorization();` after
`UseCors`. Mark `MapHealthChecks("/health")` with `.AllowAnonymous()`.

NuGet additions for the API project:
`Microsoft.AspNetCore.Authentication.JwtBearer 9.0.0`.

### T005 - Local stub AuthenticationHandler

Create
`backend/src/HomeMaintenance.Infrastructure/Auth/DevStubAuthenticationHandler.cs`:

```csharp
public sealed class DevStubAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevStubAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var auth))
            return Task.FromResult(AuthenticateResult.NoResult());

        var raw = auth.ToString();
        const string prefix = "Bearer dev-";
        if (!raw.StartsWith(prefix, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());

        var sub = raw[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(sub))
            return Task.FromResult(
                AuthenticateResult.Fail("Empty dev-stub subject"));

        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", sub),
            new Claim(ClaimTypes.NameIdentifier, sub),
        }, "DevStub");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "DevStub");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

### T006 - Startup assertion blocks stub in production

Already implemented inside `AddAppAuthentication` (T004). Add a
dedicated integration test in WP01 (T007) that boots the API in
`Production` mode with `Auth:UseStub=true` and asserts a startup
exception.

### T007 - Integration tests for the auth surface

In `backend/tests/HomeMaintenance.Integration.Tests/Auth/`:

- `AnonymousRequest_To_AnyApiRoute_Returns401` - hit any /api path
  (use `/api/_authping` if it does not exist, add a temporary
  authenticated test endpoint inside `WebApplicationFactory`); assert
  `401 Unauthorized`.
- `Health_AllowsAnonymous_Returns200` - hit `/health` without a token;
  assert 200.
- `DevStub_AcceptsBearer_dev_alice` - hit a temporary
  `[Authorize]`-protected endpoint with `Authorization: Bearer dev-alice`
  in Development; assert 200 and a body that surfaces the resolved
  OwnerId.
- `Stub_In_Production_FailsStartup` - boot the API with
  `ASPNETCORE_ENVIRONMENT=Production` and `Auth:UseStub=true`; assert
  `WebApplicationFactory.CreateClient()` throws an exception whose
  inner message names `Auth:UseStub`.

Extend `ApiFactory` to support overriding configuration per test
(`ConfigureAppConfiguration` to inject in-memory configuration).

## Test strategy

- Unit: every public Result API, every Error variant, OwnerId
  validation, OwnerId equality.
- Integration: 401 default-deny, /health allow-anonymous, dev-stub
  resolution, production-stub-blocked.
- No frontend in this WP.

## Definition of Done

- [ ] All seven subtasks merged in a single PR.
- [ ] `dotnet build` is clean (no warnings, TreatWarningsAsErrors).
- [ ] `dotnet test` green on both unit and integration suites.
- [ ] CI workflow passes.
- [ ] The PR description points reviewers at `research.md` R3-R5 for
      the design rationale.

## Risks and non-obvious bits

- `JwtBearerHandler` uses `NameClaimType = "sub"` so that
  `User.Identity.Name` resolves to the Google sub. The handler also
  populates `ClaimTypes.NameIdentifier`; `HttpContextIdentityProvider`
  reads both for resilience.
- The `FallbackPolicy` is what makes default-deny work without
  decorating every endpoint. `/health` is the only public route and is
  explicitly marked `AllowAnonymous`.
- Integration tests need `ASPNETCORE_ENVIRONMENT` set per test;
  `WebApplicationFactory` already supports
  `UseEnvironment(...)` for that.
- The `DevStub` scheme name is used as a string literal in three
  places (handler ctor base, AddScheme, AuthenticationTicket). Pull
  into a `const string` to avoid drift.

## Next command

```
polaris implement WP01
```

## Activity Log

- 2026-05-14T14:18:31Z – claude – shell_pid=62492 – lane=doing – Assigned agent via workflow command
- 2026-05-24T00:00:00Z – claude – lane=done – All subtasks T001-T007 completed and merged
