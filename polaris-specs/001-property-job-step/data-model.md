# Data Model: 001-property-job-step

Phase 1 of `/polaris.plan`. The canonical shape of Domain entities,
Application DTOs, and MongoDB documents.

## Domain layer

### OwnerId (value object)

```csharp
namespace HomeMaintenance.Domain.Identity;

public sealed record OwnerId
{
    public string Value { get; }

    public OwnerId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public static implicit operator string(OwnerId id) => id.Value;
    public override string ToString() => Value;
}
```

Equality is by `Value`. `OwnerId` wraps the OIDC `sub` claim; the Domain
never sees the raw string.

### Property (aggregate root)

```csharp
namespace HomeMaintenance.Domain.Properties;

public sealed class Property : Entity
{
    public OwnerId Owner { get; private set; }
    public string Name { get; private set; }

    private Property(string id, OwnerId owner, string name) : base(id)
    {
        Owner = owner;
        Name = name;
    }

    public static Property Create(string id, OwnerId owner, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 100)
            throw new ArgumentException("Name length must be 1..100", nameof(name));
        return new Property(id, owner, name.Trim());
    }

    public void Rename(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        if (newName.Length > 100)
            throw new ArgumentException("Name length must be 1..100", nameof(newName));
        Name = newName.Trim();
    }
}
```

Invariants:
- `Owner` is immutable after creation.
- `Name` is non-empty, trimmed, max 100 chars (FR-009, FR-012).
- Construction is via the static factory; the constructor is `private`.

### Job (aggregate root) and Step (child entity)

```csharp
namespace HomeMaintenance.Domain.Jobs;

public enum JobStatus { Active, Completed }

public sealed class Job : Entity
{
    private readonly List<Step> _steps = new();

    public OwnerId Owner { get; private set; }
    public string PropertyId { get; private set; }
    public string Name { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public JobStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public IReadOnlyList<Step> Steps => _steps.AsReadOnly();

    private Job(string id, OwnerId owner, string propertyId, string name,
                DateOnly? dueDate) : base(id)
    {
        Owner = owner;
        PropertyId = propertyId;
        Name = name;
        DueDate = dueDate;
        Status = JobStatus.Active;
        CompletedAt = null;
    }

    public static Job Create(
        string id,
        OwnerId owner,
        string propertyId,
        string name,
        DateOnly? dueDate,
        IEnumerable<string> initialStepDescriptions)
    {
        ValidateName(name);
        var job = new Job(id, owner, propertyId, NormaliseName(name), dueDate);
        var order = 0;
        foreach (var desc in initialStepDescriptions)
        {
            job._steps.Add(Step.Create(NewStepId(), order++, desc));
        }
        return job;
    }

    public void Rename(string newName)
    {
        EnsureActive();
        ValidateName(newName);
        Name = NormaliseName(newName);
    }

    public void SetDueDate(DateOnly? dueDate)
    {
        EnsureActive();
        DueDate = dueDate;
    }

    public Step AddStep(string description)
    {
        EnsureActive();
        var step = Step.Create(NewStepId(), _steps.Count, description);
        _steps.Add(step);
        return step;
    }

    public Result<None> RemoveStep(string stepId)
    {
        EnsureActive();
        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null)
            return Result<None>.Failure(new NotFoundError("Step", stepId));
        _steps.Remove(step);
        RenumberSteps();
        return Result<None>.Success(None.Value);
    }

    public Result<None> ReorderSteps(IReadOnlyList<string> orderedStepIds)
    {
        EnsureActive();
        if (orderedStepIds.Count != _steps.Count)
            return Result<None>.Failure(
                new ValidationError(nameof(orderedStepIds),
                    "Must list every existing step id exactly once"));

        var ordered = new List<Step>(orderedStepIds.Count);
        foreach (var id in orderedStepIds)
        {
            var step = _steps.FirstOrDefault(s => s.Id == id);
            if (step is null)
                return Result<None>.Failure(
                    new ValidationError(nameof(orderedStepIds),
                        $"Unknown step id {id}"));
            ordered.Add(step);
        }
        _steps.Clear();
        _steps.AddRange(ordered);
        RenumberSteps();
        return Result<None>.Success(None.Value);
    }

    public Result<None> EditStepDescription(string stepId, string newDescription)
    {
        EnsureActive();
        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null)
            return Result<None>.Failure(new NotFoundError("Step", stepId));
        step.EditDescription(newDescription);
        return Result<None>.Success(None.Value);
    }

    public Result<None> TickStep(string stepId, DateTime now)
    {
        EnsureActive();
        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null)
            return Result<None>.Failure(new NotFoundError("Step", stepId));
        step.Tick(now);
        return Result<None>.Success(None.Value);
    }

    public Result<None> UntickStep(string stepId)
    {
        EnsureActive();
        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null)
            return Result<None>.Failure(new NotFoundError("Step", stepId));
        step.Untick();
        return Result<None>.Success(None.Value);
    }

    public Result<None> Complete(DateTime now)
    {
        if (Status == JobStatus.Completed)
            return Result<None>.Failure(
                new BusinessRuleError("job_already_completed",
                    "Job is already completed"));
        if (_steps.Count == 0)
            return Result<None>.Failure(
                new BusinessRuleError("job_has_no_steps",
                    "Job has no steps; nothing to complete"));
        if (_steps.Any(s => !s.IsCompleted))
            return Result<None>.Failure(
                new BusinessRuleError("steps_incomplete",
                    "Not all steps are completed"));
        Status = JobStatus.Completed;
        CompletedAt = now;
        return Result<None>.Success(None.Value);
    }

    // Internal helpers
    private void EnsureActive()
    {
        if (Status != JobStatus.Active)
            throw new InvalidOperationException("Job is sealed");
    }
    private static void ValidateName(string name) { /* trim + length check */ }
    private static string NormaliseName(string name) => name.Trim();
    private void RenumberSteps()
    {
        for (var i = 0; i < _steps.Count; i++) _steps[i].SetOrder(i);
    }
    private static string NewStepId() => Guid.NewGuid().ToString("N");
}

public sealed class Step : Entity
{
    public int Order { get; private set; }
    public string Description { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private Step(string id, int order, string description) : base(id)
    {
        Order = order;
        Description = description;
        IsCompleted = false;
        CompletedAt = null;
    }

    public static Step Create(string id, int order, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (description.Length > 500)
            throw new ArgumentException("Description length must be 1..500");
        return new Step(id, order, description.Trim());
    }

    internal void SetOrder(int order) => Order = order;

    internal void EditDescription(string newDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newDescription);
        if (newDescription.Length > 500)
            throw new ArgumentException("Description length must be 1..500");
        Description = newDescription.Trim();
    }

    internal void Tick(DateTime now)
    {
        if (IsCompleted) return; // idempotent
        IsCompleted = true;
        CompletedAt = now;
    }

    internal void Untick()
    {
        if (!IsCompleted) return; // idempotent
        IsCompleted = false;
        CompletedAt = null;
    }
}
```

Invariants on Job and Step:
- `EnsureActive()` is the single chokepoint; any mutation when
  `Status == Completed` throws `InvalidOperationException`. The API layer
  never reaches this because use-case handlers check `Status` first and
  return a `BusinessRuleError`. The exception is a defence-in-depth
  guard.
- `_steps` is contiguous from 0; `RenumberSteps()` runs after any remove
  or reorder.
- `Step.Tick`, `Step.Untick`, `Step.EditDescription`, `Step.SetOrder` are
  `internal` so only the `Job` aggregate calls them. Tests that need to
  exercise Step directly use `InternalsVisibleTo`.

## Application layer

### DTOs (records)

```csharp
public sealed record PropertyDto(string Id, string Name);

public sealed record JobSummaryDto(
    string Id,
    string PropertyId,
    string Name,
    DateOnly? DueDate,
    JobStatus Status,
    DateTime? CompletedAt,
    int StepCount,
    int CompletedStepCount);

public sealed record JobDetailDto(
    string Id,
    string PropertyId,
    string Name,
    DateOnly? DueDate,
    JobStatus Status,
    DateTime? CompletedAt,
    IReadOnlyList<StepDto> Steps);

public sealed record StepDto(
    string Id,
    int Order,
    string Description,
    bool IsCompleted,
    DateTime? CompletedAt);

// Request DTOs
public sealed record CreatePropertyRequest(
    [property: Required, StringLength(100, MinimumLength = 1)]
    string Name);

public sealed record RenamePropertyRequest(
    [property: Required, StringLength(100, MinimumLength = 1)]
    string Name);

public sealed record CreateJobRequest(
    [property: Required, StringLength(200, MinimumLength = 1)]
    string Name,
    DateOnly? DueDate,
    IReadOnlyList<CreateJobStepRequest> Steps);

public sealed record CreateJobStepRequest(
    [property: Required, StringLength(500, MinimumLength = 1)]
    string Description);

public sealed record RenameJobRequest(
    [property: Required, StringLength(200, MinimumLength = 1)]
    string Name);

public sealed record SetJobDueDateRequest(DateOnly? DueDate);

public sealed record AddStepRequest(
    [property: Required, StringLength(500, MinimumLength = 1)]
    string Description);

public sealed record EditStepDescriptionRequest(
    [property: Required, StringLength(500, MinimumLength = 1)]
    string Description);

public sealed record ReorderStepsRequest(
    [property: Required, MinLength(1)]
    IReadOnlyList<string> OrderedStepIds);
```

### Repository interfaces

```csharp
public interface IPropertyRepository
{
    Task<Property?> GetAsync(string id, OwnerId owner, CancellationToken ct);
    Task<IReadOnlyList<Property>> ListAsync(OwnerId owner, CancellationToken ct);
    Task AddAsync(Property property, CancellationToken ct);
    Task UpdateAsync(Property property, CancellationToken ct);
}

public interface IJobRepository
{
    Task<Job?> GetAsync(string id, OwnerId owner, CancellationToken ct);
    Task<IReadOnlyList<Job>> ListAsync(
        OwnerId owner,
        string? propertyId,
        CancellationToken ct);
    Task AddAsync(Job job, CancellationToken ct);
    Task UpdateAsync(Job job, CancellationToken ct);
}
```

Both repositories ALWAYS filter by `ownerId`. Passing a job/property id
that is owned by another user returns `null`, which the use case maps to
`NotFoundError` (and therefore HTTP 404 per the no-leak rule).

### IIdentityProvider

```csharp
public interface IIdentityProvider
{
    /// <summary>
    /// Returns the validated principal for the inbound request. The API
    /// middleware is responsible for token validation; this abstraction
    /// only exposes the resolved OwnerId to handlers.
    /// </summary>
    OwnerId CurrentOwner { get; }
}
```

Backed by a scoped `HttpContextIdentityProvider` in Infrastructure that
reads the OwnerId from the `sub` claim populated by JWT validation.

### IAuditLog

(See `research.md` R7.)

## Infrastructure layer (MongoDB documents)

### PropertyDocument

```csharp
internal sealed class PropertyDocument
{
    [BsonId, BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = "";

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = "";

    [BsonElement("name")]
    public string Name { get; set; } = "";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
```

Collection: `properties`
Indexes:
- `{ ownerId: 1 }` (mandatory; supports the ownership filter)
- `{ ownerId: 1, name: 1 }` (supports the alphabetical list view)

### JobDocument (with embedded StepDocument)

```csharp
internal sealed class JobDocument
{
    [BsonId, BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = "";

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = "";

    [BsonElement("propertyId")]
    public string PropertyId { get; set; } = "";

    [BsonElement("name")]
    public string Name { get; set; } = "";

    [BsonElement("dueDate"), BsonIgnoreIfNull]
    public DateOnly? DueDate { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = nameof(JobStatus.Active);

    [BsonElement("completedAt"), BsonIgnoreIfNull]
    public DateTime? CompletedAt { get; set; }

    [BsonElement("steps")]
    public List<StepDocument> Steps { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

internal sealed class StepDocument
{
    [BsonElement("id")]
    public string Id { get; set; } = "";

    [BsonElement("order")]
    public int Order { get; set; }

    [BsonElement("description")]
    public string Description { get; set; } = "";

    [BsonElement("isCompleted")]
    public bool IsCompleted { get; set; }

    [BsonElement("completedAt"), BsonIgnoreIfNull]
    public DateTime? CompletedAt { get; set; }
}
```

Collection: `jobs`
Indexes:
- `{ ownerId: 1 }` (mandatory)
- `{ ownerId: 1, propertyId: 1 }` (supports the by-property list view)
- `{ ownerId: 1, status: 1 }` (cheap filter for "active jobs only" in the UI)

DateOnly is supported natively by MongoDB.Driver 3.x via the
`DateOnly` BSON serialiser; if MongoDB.Driver 3.1.0 does not include it
by default we register a `DateOnlySerializer` in `MongoDbContext` startup.

### Document <-> Domain mappers

A small set of extension methods (`PropertyDocument.ToDomain()`,
`Property.ToDocument()`, same for Job/Step). Mappers live in the
`Infrastructure.Persistence` namespace; the Domain has no knowledge of
the documents.

## Concurrency, transactions, indexes

- No multi-document transactions. Each use case writes one document.
- All write operations use `findOneAndUpdate` with `ownerId` in the
  filter, which keeps ownership enforced inside the query (defence in
  depth against a bug in the handler).
- Index creation is idempotent at startup: the `MongoDbContext`
  registers index commands on first access; we only ever create
  indexes that match the queries listed above.

## State diagrams

### Job lifecycle

```
[ create ]
     |
     v
+--Active--+      EnsureActive() blocks any mutation in Completed
|          |
| AddStep, RemoveStep, ReorderSteps, EditStepDescription,
| TickStep, UntickStep, Rename, SetDueDate     (all loop back to Active)
|          |
| Complete (when all steps are completed and step count > 0)
|          |
|          v
+-Completed (terminal; no transitions in Slice 1)
```

### Step lifecycle

```
[ created with IsCompleted=false, CompletedAt=null ]
     |
   Tick(now)
     v
[ IsCompleted=true, CompletedAt=now ]
     |
   Untick()
     v
[ IsCompleted=false, CompletedAt=null ]
```

## Validation checklist (Phase 1 -> tasks)

- [ ] Every Domain entity has a unit test covering every invariant.
- [ ] Every use case has a handler test that asserts the success and
      every failure branch (NotFound, Validation, BusinessRule).
- [ ] Every repository has an integration test covering filter-by-owner
      and update-returns-latest behaviour.
- [ ] Cross-owner access tests assert 404 (not 403) at the API layer.
