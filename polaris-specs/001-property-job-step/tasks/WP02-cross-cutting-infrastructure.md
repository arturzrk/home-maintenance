---
work_package_id: WP02
lane: "done"
dependencies: [WP01]
base_branch: 001-property-job-step-WP01
base_commit: e70515a6fa348c82bbdbcd87c47e740325df033a
created_at: '2026-05-14T15:25:02.308366+00:00'
subtasks: [T008, T009, T010, T011, T012, T013, T014]
test_status: required
test_file: tests/e2e/WP02-wp02-cross-cutting-infrastructure.e2e.js
domain: backend-logic
shell_pid: "82073"
---

# WP02 - Cross-cutting infrastructure

## Objective

Land the cross-cutting plumbing every business endpoint will share: the
audit log producer, request-scoped correlation IDs, and the
Result-to-HTTP error translator. Dormant by itself (no handler is wired
yet), but every later WP relies on it being in place.

## Inputs

- Spec: `polaris-specs/001-property-job-step/spec.md` (Audit Logging,
  Threat Surface).
- Plan: `plan.md` (Constitution Check rows "Audit logging" and
  "AuthN/AuthZ"; structure section for `AuditLog/` and `Middleware/`).
- Research: `research.md` R5 (Error -> HTTP), R7 (FileAuditLog).
- Contracts: `contracts/README.md` (RFC 7807 problem-details shape and
  error-code -> status mapping).

## Subtasks

### T008 - IAuditLog interface + AuditEvent record

Create
`backend/src/HomeMaintenance.Application/Common/Interfaces/IAuditLog.cs`:

```csharp
public interface IAuditLog
{
    Task RecordAsync(AuditEvent evt, CancellationToken ct = default);
}

public sealed record AuditEvent(
    string EventType,
    string Actor,
    string? Target,
    DateTime Timestamp,
    string CorrelationId,
    IReadOnlyDictionary<string, object?>? Payload = null);
```

`EventType` strings are constants. Add a static class
`AuditEventTypes` next to `IAuditLog` with the names from
`spec.md` "Audit Logging" (`property.created`, `job.completed`, etc.).

### T009 - FileAuditLog

Create
`backend/src/HomeMaintenance.Infrastructure/AuditLog/FileAuditLog.cs`:

```csharp
public sealed class FileAuditLog : IAuditLog
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public FileAuditLog(IOptions<AuditLogOptions> options)
    {
        _path = options.Value.SinkPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public async Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(evt, JsonOptions);
        await _gate.WaitAsync(ct);
        try
        {
            await File.AppendAllLinesAsync(_path, new[] { line }, ct);
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed class AuditLogOptions
{
    public const string SectionName = "AuditLog";
    public string SinkPath { get; set; } = "audit-trail/property-job-step.jsonl";
}
```

Register in `Infrastructure.AddInfrastructure`:
```csharp
services.Configure<AuditLogOptions>(
    configuration.GetSection(AuditLogOptions.SectionName));
services.AddSingleton<IAuditLog, FileAuditLog>();
```

### T010 - CorrelationIdMiddleware

Create
`backend/src/HomeMaintenance.API/Middleware/CorrelationIdMiddleware.cs`:

```csharp
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var id = ctx.Request.Headers.TryGetValue(HeaderName, out var existing)
                 && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("N");

        ctx.Items["CorrelationId"] = id;
        ctx.Response.Headers[HeaderName] = id;

        using (LoggerExtensions.BeginScope(
            ctx.RequestServices.GetRequiredService<ILogger<CorrelationIdMiddleware>>(),
            new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await _next(ctx);
        }
    }
}

public static class CorrelationIdExtensions
{
    public static string GetCorrelationId(this HttpContext ctx)
        => ctx.Items["CorrelationId"] as string ?? string.Empty;
}
```

Register before `UseAuthentication()` in `Program.cs`:
```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
```

### T011 - Result -> RFC 7807 problem-details translator

Create
`backend/src/HomeMaintenance.API/Middleware/AuthErrorTranslator.cs`:

```csharp
public static class ResultToHttpExtensions
{
    public static IResult ToHttp<T>(this Result<T> result, HttpContext ctx)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);
        return result.Error!.ToProblem(ctx);
    }

    public static IResult ToHttpCreated<T>(this Result<T> result, HttpContext ctx, string location)
    {
        if (result.IsSuccess) return Results.Created(location, result.Value);
        return result.Error!.ToProblem(ctx);
    }

    public static IResult ToHttpNoContent<T>(this Result<T> result, HttpContext ctx)
    {
        if (result.IsSuccess) return Results.NoContent();
        return result.Error!.ToProblem(ctx);
    }

    private static IResult ToProblem(this Error error, HttpContext ctx)
    {
        var (status, title) = error switch
        {
            NotFoundError => (StatusCodes.Status404NotFound, "Not Found"),
            ValidationError => (StatusCodes.Status400BadRequest, "Invalid Request"),
            BusinessRuleError => (StatusCodes.Status400BadRequest, "Business Rule Violation"),
            UnauthorizedError => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ForbiddenError => (StatusCodes.Status404NotFound, "Not Found"), // R5: no leak
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error"),
        };

        return Results.Problem(
            type: $"https://home-maintenance/errors/{error.Code}",
            title: title,
            statusCode: status,
            detail: error.Message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["correlationId"] = ctx.GetCorrelationId(),
            });
    }
}
```

### T012 - Gitignore audit-trail/

Append to `.gitignore` (find a good spot in the auto-managed Polaris
block or near the bottom):

```
# Audit log local sink (Slice 1: file-backed; production uses a managed sink)
audit-trail/
```

`FileAuditLog` already calls `Directory.CreateDirectory(...)` lazily,
so no separate startup step is needed.

### T013 [P] - FileAuditLog unit tests

In
`backend/tests/HomeMaintenance.Unit.Tests/Infrastructure/AuditLog/`:

- `Append_WritesValidJsonLine` - record one event, read the file,
  parse the line as `AuditEvent`, assert values round-trip.
- `Append_ConcurrentCalls_AllRecordsPresent` - fire 100 concurrent
  `RecordAsync` calls; verify the file ends up with exactly 100 lines
  and each is valid JSON.
- `Append_AfterRestart_AppendsNotTruncates` - construct two
  `FileAuditLog` instances pointing at the same path; record one
  event in each; assert both events are present in the file in order.

Use a temp directory per test (`Path.GetTempFileName()` + `.jsonl`).

### T014 - Integration test: 404 leaks correlationId

Once a business endpoint exists (will be added by WP03), extend the
sealing test to cover the correlation id surface. For WP02 in
isolation, add a temporary "echo problem-details" test endpoint inside
`ApiFactory` (gated to test builds) that returns a `Result<None>`
failure with a `ForbiddenError`. Assert the JSON body contains
`code: "forbidden"` mapped to status 404 and that `correlationId`
matches the `X-Correlation-Id` header.

This dies after WP03 lands; remove the temporary endpoint in WP03 and
move the assertion into the real Property endpoint integration tests.

## Test strategy

- Unit: FileAuditLog (concurrency, format, restart).
- Integration: middleware wiring (header round-trip, problem-details
  shape).
- No frontend in this WP.

## Definition of Done

- [ ] Audit log records to `audit-trail/property-job-step.jsonl` in
      Development (verified manually with `tail -f`).
- [ ] Every problem-details response carries `code` and
      `correlationId`.
- [ ] CI green.
- [ ] PR description references `research.md` R5 (Error mapping) and
      R7 (FileAuditLog).

## Risks and non-obvious bits

- `Results.Problem` extensions are flattened into the JSON root, so the
  body is exactly the shape documented in `contracts/README.md`.
- `LoggerExtensions.BeginScope(...)` adds the correlation id to every
  log line emitted during the request, which makes future debugging
  trivially traceable.
- Adding `audit-trail/` to `.gitignore` does NOT remove it if a
  developer has already created it locally; that is fine since the
  directory is generated.
- 401 responses from `JwtBearerHandler` bypass the translator (they go
  through ASP.NET Core's built-in challenge). They still need to
  surface a JSON body with `code: "unauthorized"`. Add an
  `OnChallenge` event in the JWT bearer options to write the same
  problem-details shape; this is small enough to include here.

## Next command

```
polaris implement WP02 --base WP01
```

## Activity Log

- 2026-05-14T15:25:02Z – claude – shell_pid=82073 – lane=doing – Assigned agent via workflow command
- 2026-05-24T00:00:00Z – claude – lane=done – All subtasks T008-T014 completed and merged
