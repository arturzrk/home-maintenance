# Research: 001-property-job-step

Phase 0 of `/polaris.plan`. Resolves the open questions in `spec.md` and
captures decisions that propagate through Phase 1 (data-model, contracts,
quickstart) and into the work packages.

## R1. Frontend OAuth handshake: NextAuth vs raw next-auth/google

### Decision

Use **NextAuth (Auth.js) v5** with the Google provider, deployed via the
`app/api/auth/[...nextauth]/route.ts` route handler.

### Rationale

- NextAuth is the canonical Next.js OIDC client; it owns the cookie session,
  CSRF token, and JWE encryption. Rolling our own (`raw next-auth/google` is
  itself a misnomer - it means handcrafting OAuth2 + PKCE + JWKS verification)
  would duplicate ~500 lines of well-tested code for no win.
- NextAuth's `getServerSession` works inside Server Components, which keeps
  our default rendering strategy intact. Calls from Server Components fetch
  the API with the Google ID token attached.
- App Router parity: NextAuth v5 ships first-class support for the App Router
  via the new `auth.ts` config + route handler pattern.

### Alternatives considered

- **Direct Google OAuth2 + manual JWKS validation** in custom middleware.
  Rejected: more code, more attack surface, no benefit. NextAuth already does
  exactly this internally.
- **Authentik / Keycloak as IdP proxy**. Out of scope: introduces an extra
  service for a personal-scale app.
- **Clerk / Auth0 hosted auth**. Out of scope: paid SaaS dependency.

### Implementation notes

- Pin `next-auth@^5.0.0` in `frontend/package.json`.
- The NextAuth session callback MUST attach the raw Google ID token to the
  session object (`session.idToken`) so the typed API client can forward it
  as a Bearer token. The default session shape does not include the ID token;
  override the `jwt` and `session` callbacks.
- `NEXTAUTH_SECRET`, `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, and
  `NEXTAUTH_URL` go in `.env.local`; `.env.example` documents them with
  empty values.
- Local dev MAY skip Google and use the API's stub identity provider directly
  by hitting endpoints with `Authorization: Bearer dev-<sub>` (the stub
  parses everything after `dev-` as the OwnerId). NextAuth is only required
  for the full sign-in flow.

### Risks

- NextAuth v5 is the breaking-change line; pin to `5.x.x` patch and test on
  every minor bump.
- Google ID tokens default to 1-hour expiry; the NextAuth `jwt` callback must
  trigger a refresh before expiry, otherwise the API gets a 401 mid-session
  and the frontend has to handle the silent re-auth.

## R2. Steps storage: embedded in Job document vs separate collection

### Decision

**Embed** Step entities as an array inside the `JobDocument`.

### Rationale

- Steps are aggregate-children of Job; the constitution and DDD discipline
  both say child entities load and save with their root. Splitting them into
  a separate collection would require a second round-trip on every read
  (or a `$lookup`) for no semantic benefit.
- The MongoDB 16 MB BSON document limit is comfortable: a step description
  is capped at 500 chars (FR-024), and a worst-case 100-step Job is
  ~50 KB. We are 3 orders of magnitude under the limit.
- Atomic updates: marking a step done and updating Job state can happen in a
  single `findOneAndUpdate` call on the Job document. With a separate
  collection we would need a transaction or an inconsistency window.
- Querying: ListJobs returns a list view (no steps shown); we project the
  document with `steps: 0` to keep the payload small. GetJob returns the
  full document including embedded steps.

### Alternatives considered

- **Separate `steps` collection keyed by `jobId`**: rejected as documented
  above. Would only make sense if steps were independently queryable across
  Jobs, which the spec does not require.

### Implementation notes

- `JobDocument` carries `Steps: List<StepDocument>` mapped from the Mongo
  `steps` BSON array. Index on `ownerId` and `propertyId` only - no index on
  steps array elements.
- The `JobRepository` exposes typed methods (`AddStep`, `RemoveStep`,
  `TickStep`, etc.) that issue scoped `$set`/`$pull`/`$push` operations
  against the steps array, not a full document replacement. This both
  prevents lost-update races on unrelated fields and keeps the wire
  payload small.
- Step ordering in storage matches the canonical `Order` field; on read,
  the repository sorts by `Order` ascending to defend against any caller
  that mutated the array in-place.

### Risks

- If we later want a cross-Job "all overdue steps" query (e.g., a dashboard),
  embedded steps make it expensive (`$unwind` + `$match`). Note this as a
  future risk; revisit when such a query is actually needed.

## R3. Google JWKS caching window

### Decision

Cache Google's signing keys via the default behaviour of
`Microsoft.AspNetCore.Authentication.JwtBearer`: in-memory cache with
**24-hour refresh interval** and automatic refresh on first 401 due to
unknown `kid`.

### Rationale

- Google rotates JWKs periodically (typically every few days). Caching for
  24h is the recommended default and is what the ASP.NET Core JWT library
  does out of the box (`AutomaticRefreshInterval = 24h`,
  `RefreshOnIssuerKeyNotFound = true`).
- Shorter intervals (e.g., 1 hour) increase outbound traffic to Google
  without measurable security benefit; longer intervals risk users seeing
  spurious 401s during a key-rotation window.
- The library refreshes immediately if a token's `kid` is not in the cache,
  so rotations beat the schedule transparently.

### Alternatives considered

- **Hard-coded short refresh (e.g., 5 min)**: rejected. No security benefit
  given JWT signature validation is cryptographic, not based on freshness.
- **No cache at all (fetch JWKS on every request)**: rejected. Self-DOS.
- **Background pre-warming task**: rejected. Premature optimisation;
  cold-start is one HTTP round-trip and only on the first request after
  process start.

### Implementation notes

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://accounts.google.com";
        options.Audience = configuration["Auth:Google:ClientId"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://accounts.google.com",
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(60),
        };
        options.RefreshOnIssuerKeyNotFound = true;
        options.AutomaticRefreshInterval = TimeSpan.FromHours(24);
    });
```

ClockSkew is 60s (default 5min is too lenient; 0s is brittle).

## R4. Local-dev OIDC stub strategy

### Decision

Implement a **header-based stub** that accepts `Authorization: Bearer dev-<sub>`
when `Auth:UseStub` is true (only in `Development`). The stub parses everything
after `dev-` as the OwnerId and short-circuits the authentication middleware.

### Rationale

- No external dependency for local development (no Google client setup, no
  Docker IdP).
- Each developer picks their own `dev-` sub by editing one config value,
  which avoids stepping on each other's data in shared dev databases.
- Production refuses stub tokens unconditionally (the option is bound to
  the `IWebHostEnvironment.IsDevelopment()` check at startup).

### Alternatives considered

- **Dockerised Keycloak / Authentik locally**: works but heavyweight for
  this stage; revisit when we need to exercise actual OAuth flows in
  local integration tests.
- **MockJwtBearerHandler in tests only**: same intent but only at the
  integration-test level, leaves Swagger / browser dev unstubbed.

### Implementation notes

- The stub is implemented as a custom `AuthenticationHandler` registered
  in `Infrastructure.AddAuthentication(...)`.
- Configuration shape:
  ```
  Auth:
    UseStub: true            # ONLY ever true in appsettings.Development.json
    Google:
      ClientId: "..."
      Authority: "https://accounts.google.com"
  ```
- A startup check ASSERTS that `UseStub == false` when the environment is
  Production, throwing on app start otherwise. Caught by an integration test.

### Risks

- Easy to leave `UseStub: true` accidentally in a non-dev environment;
  the startup assertion is the safety net.

## R5. Result<T> pattern - introduce now or wait?

### Decision

Introduce `Result<T>` now, in `HomeMaintenance.Application/Common/Result.cs`.
The constitution permits adding it "when first needed". Slice 1's
`CompleteJob` rejects the "any step incomplete" case as a business-rule
failure, which is the canonical case for Result over exceptions.

### Rationale

- The spec's FR-018 explicitly says "returns failure, not exception". This
  requires a non-exception return path.
- Doing it once, at the start of Slice 1, avoids a per-handler ad-hoc
  return shape (mixing exceptions and Try-patterns) that would have to be
  retrofitted on every handler later.

### Shape

```csharp
public readonly record struct Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error) => new(default, error);
}

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
```

The API layer maps `Error.Code` to an HTTP status code via a single helper
in `Middleware/AuthErrorTranslator.cs`:

| Code | Status |
|---|---|
| `not_found` | 404 |
| `validation` | 400 |
| `business_rule` | 400 (with error code in body) |
| `unauthorized` | 401 |
| `forbidden` | 404 (per spec edge cases - no leak) |

### Alternatives considered

- **Throw `DomainException`s and catch at the API layer**: violates the
  constitution's "no exceptions for control flow" rule.
- **`OneOf<T, Error>` library**: extra dependency for ergonomic sugar
  that we do not need at this scale.

## R6. DTO validation strategy

### Decision

Use **built-in `System.ComponentModel.DataAnnotations`** with
`MiniValidation` (a tiny ~3 KB library that validates an object against
its data-annotation attributes) for Minimal API endpoints.

### Rationale

- Records with `[Required]`, `[StringLength]`, `[Range]` attributes give us
  the validations we need (name length, description length, etc.) with no
  custom framework.
- `MiniValidation.TryValidate(dto, out errors)` is a one-liner per endpoint
  that maps cleanly to a `ValidationError` Result.
- FluentValidation is more powerful but introduces a non-trivial dependency
  and a per-DTO Validator class for not much gain at Slice 1's scope.

### Alternatives considered

- **FluentValidation**: revisit when we need cross-field rules or async
  validation that DataAnnotations cannot express.
- **Pure manual validation in handlers**: works but couples validation to
  business logic; record-level attributes keep it declarative.

## R7. Audit log writer

### Decision

A simple `FileAuditLog : IAuditLog` that appends one JSON record per line
to `audit-trail/property-job-step.jsonl`, with a `File.AppendAllText`-style
write under a `SemaphoreSlim` so concurrent writes are serialised.

### Rationale

- Local-only deployment in Slice 1: a managed sink (Splunk, Sentinel, S3 +
  Object Lock) is overkill and unavailable.
- `audit-trail/` is gitignored to keep the repo clean and to prevent
  accidental disclosure of session metadata.
- A single semaphore is fine at ~10 concurrent users; under heavier load we
  would batch writes via a Channel-backed worker, but that is premature now.
- The interface (`IAuditLog`) keeps the producer side identical when a
  future slice swaps in a real sink.

### Shape

```csharp
public interface IAuditLog
{
    Task RecordAsync(AuditEvent evt, CancellationToken ct = default);
}

public sealed record AuditEvent(
    string EventType,         // "auth.signin_success", "job.completed", etc.
    string Actor,             // OwnerId or "anonymous"
    string? Target,           // "{resourceType}:{id}" or null
    DateTime Timestamp,       // UTC
    string CorrelationId,
    IReadOnlyDictionary<string, object?> Payload);
```

### Risks

- Disk-full failures crash the writer. Acceptable for local dev; a
  production sink would have retry / DLQ.

## Summary of decisions

| # | Decision |
|---|---|
| R1 | NextAuth v5 with Google provider; ID token forwarded as Bearer to API |
| R2 | Embed Steps inside JobDocument; one collection per aggregate |
| R3 | JWKS cache: 24h refresh + `RefreshOnIssuerKeyNotFound`, 60s clock skew |
| R4 | Header-based local OIDC stub (`Bearer dev-<sub>`), Development env only, startup assertion blocks production use |
| R5 | Introduce `Result<T>` + typed `Error` records in Application; API maps to HTTP via single helper |
| R6 | DataAnnotations + MiniValidation for DTO validation |
| R7 | `FileAuditLog` appends JSONL to `audit-trail/property-job-step.jsonl`, semaphore-serialised writes |
