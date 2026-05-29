---
work_package_id: WP03
lane: "done"
dependencies: [WP02]
base_branch: 001-property-job-step-WP02
base_commit: c6ebfd5d2a99f98313309ca18aa30bcec4f43767
created_at: '2026-05-14T17:06:29.588438+00:00'
subtasks: [T015, T016, T017, T018, T019, T020]
test_status: required
test_file: tests/e2e/WP03-wp03-property-aggregate-backend.e2e.js
domain: backend-logic
shell_pid: "13951"
agent: "claude"
assignee: "arturzrk@gmail.com"
---

# WP03 - Property aggregate backend

## Objective

Deliver the Property aggregate from Domain through to API. After this
WP, an authenticated caller can create, list, read, and rename their
Properties via REST. Frontend is WP04.

## Inputs

- Spec: FR-009..FR-013; user stories US1 (sign in + see list) and
  US2 (create); acceptance scenarios for each.
- Data model: `data-model.md` "Property" + "PropertyDocument".
- Contracts: `contracts/properties.md`.

## Subtasks

### T015 - Property domain aggregate

Create `backend/src/HomeMaintenance.Domain/Properties/Property.cs`.
Shape from `data-model.md`. Sealed class inheriting `Entity`. Private
constructor; static `Create` factory; `Rename` method.

Validations (apply in both `Create` and `Rename`):
- Throw `ArgumentException` if name is null, empty, or whitespace.
- Throw `ArgumentException` if `name.Length > 100`.
- Always store the trimmed form.

Unit tests in
`backend/tests/HomeMaintenance.Unit.Tests/Domain/Properties/`:
- `Create_WithValidName_AssignsTrimmedName`
- `Create_WithEmptyName_Throws`
- `Create_WithWhitespaceName_Throws`
- `Create_WithName101Chars_Throws`
- `Create_WithName100Chars_Succeeds`
- `Rename_WithValidName_UpdatesName`
- `Rename_WithEmptyName_Throws`
- `Rename_WithName101Chars_Throws`
- `Create_TwoPropertiesSameOwner_AreNotEqual` (different ids)

### T016 - PropertyDocument and PropertyRepository

Create
`backend/src/HomeMaintenance.Infrastructure/Persistence/Documents/PropertyDocument.cs`
matching `data-model.md`.

Create
`backend/src/HomeMaintenance.Infrastructure/Persistence/PropertyRepository.cs`
implementing `IPropertyRepository`:

```csharp
public sealed class PropertyRepository : IPropertyRepository
{
    private readonly IMongoCollection<PropertyDocument> _collection;

    public PropertyRepository(IMongoDatabase db)
    {
        _collection = db.GetCollection<PropertyDocument>("properties");
        // Indexes are created idempotently on first use via MongoDbContext bootstrap;
        // see T016b (the same file may register the indexes if not already done).
    }

    public async Task<Property?> GetAsync(string id, OwnerId owner, CancellationToken ct)
    {
        var doc = await _collection.Find(d => d.Id == id && d.OwnerId == owner.Value)
                                   .FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<IReadOnlyList<Property>> ListAsync(OwnerId owner, CancellationToken ct)
    {
        var docs = await _collection.Find(d => d.OwnerId == owner.Value)
                                    .SortBy(d => d.Name)
                                    .ToListAsync(ct);
        return docs.Select(d => d.ToDomain()).ToList();
    }

    public Task AddAsync(Property property, CancellationToken ct)
        => _collection.InsertOneAsync(property.ToDocument(), cancellationToken: ct);

    public Task UpdateAsync(Property property, CancellationToken ct)
        => _collection.ReplaceOneAsync(
            d => d.Id == property.Id && d.OwnerId == property.Owner.Value,
            property.ToDocument(), cancellationToken: ct);
}
```

Add mapper extensions in the same folder:
```csharp
internal static class PropertyMappings
{
    public static Property ToDomain(this PropertyDocument doc)
    {
        // reflection-free reconstruction via a Domain factory that
        // accepts the existing id (Property.Hydrate(id, owner, name)).
    }

    public static PropertyDocument ToDocument(this Property property)
    {
        var now = DateTime.UtcNow;
        return new PropertyDocument
        {
            Id = property.Id,
            OwnerId = property.Owner.Value,
            Name = property.Name,
            CreatedAt = now,    // overwritten only on insert; see note
            UpdatedAt = now,
        };
    }
}
```

Add an internal `Property.Hydrate(...)` factory next to `Create(...)`
that the mapper uses to reconstruct without re-running validation.
Decorate `Property` with `[InternalsVisibleTo("HomeMaintenance.Infrastructure")]`
in `Domain.csproj`.

Index creation: extend `MongoDbContext` (or add a new
`MongoIndexInitializer` registered as `IHostedService`) so on first
startup it ensures `{ ownerId: 1 }` and `{ ownerId: 1, name: 1 }`
indexes exist on the `properties` collection.

Register in `Infrastructure.DependencyInjection`:
```csharp
services.AddScoped<IPropertyRepository, PropertyRepository>();
```

### T017 - CreateProperty and RenameProperty use cases

Create
`backend/src/HomeMaintenance.Application/Properties/Commands/CreateProperty.cs`:

```csharp
public sealed record CreatePropertyCommand(string Name);

public sealed class CreatePropertyHandler
{
    private readonly IPropertyRepository _repo;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly IHttpContextAccessor _http;

    public CreatePropertyHandler(IPropertyRepository repo,
        IIdentityProvider identity, IAuditLog audit, IHttpContextAccessor http)
    {
        _repo = repo;
        _identity = identity;
        _audit = audit;
        _http = http;
    }

    public async Task<Result<PropertyDto>> Handle(
        CreatePropertyCommand cmd, CancellationToken ct)
    {
        try
        {
            var owner = _identity.CurrentOwner;
            var property = Property.Create(IdFactory.NewId(), owner, cmd.Name);
            await _repo.AddAsync(property, ct);
            await _audit.RecordAsync(new AuditEvent(
                AuditEventTypes.PropertyCreated,
                owner.Value,
                $"property:{property.Id}",
                DateTime.UtcNow,
                _http.HttpContext!.GetCorrelationId(),
                new Dictionary<string, object?> { ["name"] = property.Name }), ct);
            return Result<PropertyDto>.Success(new PropertyDto(property.Id, property.Name));
        }
        catch (ArgumentException ex)
        {
            return Result<PropertyDto>.Failure(
                new ValidationError("name", ex.Message));
        }
    }
}
```

`RenameProperty` analogous: load by id+owner, return `NotFoundError`
if null, call `Rename`, `UpdateAsync`, emit `property.renamed`
audit event with old + new name. Catch `ArgumentException` for
validation.

Add `Result<None>` flavour where there is no DTO to return (Rename can
return the updated DTO; keep API consistent and return PropertyDto).

`IdFactory` is a tiny helper in Application/Common that wraps
`Guid.NewGuid().ToString("N")`.

Unit tests in
`backend/tests/HomeMaintenance.Unit.Tests/Application/Properties/`:
- Success path: assert `AddAsync` called, audit event emitted.
- Validation: empty name returns `Result.Failure(ValidationError(...))`.
- Rename success and not-found cases. Use NSubstitute for repository
  and audit log.

### T018 - ListProperties and GetProperty queries

`ListPropertiesQuery` -> `Result<IReadOnlyList<PropertyDto>>`. Always
scoped to `CurrentOwner`.

`GetPropertyQuery(string Id)` -> `Result<PropertyDto>` or
`NotFoundError`.

Unit tests cover success and not-found-not-owned (repository returns
null for cross-owner ids; handler maps to `NotFoundError`).

### T019 - PropertyEndpoints

Create
`backend/src/HomeMaintenance.API/Endpoints/PropertyEndpoints.cs`:

```csharp
public static class PropertyEndpoints
{
    public static IEndpointRouteBuilder MapPropertyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/properties").RequireAuthorization();

        group.MapPost("", async (
            CreatePropertyRequest body,
            CreatePropertyHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidation.MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);
            var result = await handler.Handle(new CreatePropertyCommand(body.Name), ct);
            return result.ToHttpCreated(ctx, $"/api/properties/{result.Value?.Id}");
        });

        group.MapGet("", async (
            ListPropertiesHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new ListPropertiesQuery(), ct);
            return result.ToHttp(ctx);
        });

        group.MapGet("{id}", async (
            string id,
            GetPropertyHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetPropertyQuery(id), ct);
            return result.ToHttp(ctx);
        });

        group.MapPatch("{id}", async (
            string id,
            RenamePropertyRequest body,
            RenamePropertyHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidation.MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);
            var result = await handler.Handle(
                new RenamePropertyCommand(id, body.Name), ct);
            return result.ToHttp(ctx);
        });

        return app;
    }
}
```

Register handlers in
`Application.DependencyInjection.AddApplication`:
```csharp
services.AddScoped<CreatePropertyHandler>();
services.AddScoped<RenamePropertyHandler>();
services.AddScoped<ListPropertiesHandler>();
services.AddScoped<GetPropertyHandler>();
```

Add `app.MapPropertyEndpoints();` to `Program.cs`.

NuGet additions for the API project: `MiniValidation 0.9.x`.

### T020 - Integration tests

In
`backend/tests/HomeMaintenance.Integration.Tests/Properties/`:

- `Post_Property_AsAuthenticatedUser_ReturnsCreated`
- `Post_Property_EmptyName_Returns400`
- `Post_Property_NameTooLong_Returns400`
- `Get_Property_OwnedByCaller_Returns200`
- `Get_Property_OwnedByOther_Returns404` (with `correlationId` in body)
- `List_Properties_ReturnsOnlyCallersOwn`
- `Patch_Property_ValidName_Returns200`
- `Patch_Property_OwnedByOther_Returns404`
- `Post_Property_Anonymous_Returns401`
- `Audit_PropertyCreated_AppearsInLog` - capture the audit-log path
  with a temp directory via configuration override; assert the JSON
  line.

Use `dev-alice` / `dev-bob` as test owners via the DevStub.

## Test strategy

- Unit: domain invariants (every FR-009/FR-012 boundary), handler
  branches (success, validation, not-found).
- Integration: full HTTP round-trips against Testcontainers Mongo
  with dev-stub auth.

## Definition of Done

- [ ] All four endpoints respond per `contracts/properties.md`.
- [ ] Audit log shows `property.created` and `property.renamed`
      events.
- [ ] Cross-owner GET returns 404, not 403.
- [ ] CI green.

## Risks and non-obvious bits

- The internal `Property.Hydrate(...)` factory is a deliberate
  Domain concession to Infrastructure. The constitution allows it via
  `InternalsVisibleTo`. Hydration MUST NOT re-run validations, because
  storage-level data may include records created before stricter
  validation was added; failing to load them would be a worse
  outcome than holding the looser invariant.
- `MongoDbContext` already exists; add the index initialiser as an
  `IHostedService` to keep startup ordering explicit.
- `MiniValidation` is a tiny dep but new; pin to a known-good
  version (0.9.x).

## Next command

```
polaris implement WP03 --base WP02
```

## Activity Log

- 2026-05-14T17:06:29Z -- claude -- shell_pid=13951 -- lane=doing -- Assigned agent via workflow command
- 2026-05-24T00:00:00Z -- claude -- lane=done -- All subtasks T015-T020 completed and merged
