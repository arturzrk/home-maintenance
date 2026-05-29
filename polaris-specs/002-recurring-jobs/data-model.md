# Data Model: 002-recurring-jobs

## Domain layer

### CadenceUnit (enum)

```csharp
namespace HomeMaintenance.Domain.JobDefinitions;

public enum CadenceUnit { Day, Week, Month, Year }
```

### ScheduleDefinition (value object)

```csharp
namespace HomeMaintenance.Domain.JobDefinitions;

public sealed record ScheduleDefinition
{
    public CadenceUnit Unit { get; }
    public int Multiplier { get; }       // ≥ 1
    public DateOnly StartDate { get; }
    public DateOnly? EndDate { get; }    // > StartDate if set

    public ScheduleDefinition(CadenceUnit unit, int multiplier, DateOnly startDate, DateOnly? endDate = null)
    {
        if (multiplier < 1)
            throw new ArgumentException("Multiplier must be ≥ 1.", nameof(multiplier));
        if (endDate.HasValue && endDate.Value <= startDate)
            throw new ArgumentException("EndDate must be after StartDate.", nameof(endDate));
        Unit = unit;
        Multiplier = multiplier;
        StartDate = startDate;
        EndDate = endDate;
    }

    public IEnumerable<DateOnly> OccurrencesInRange(DateOnly from, DateOnly to)
    {
        for (var n = 0; ; n++)
        {
            var date = NthOccurrence(n);
            if (date > to) yield break;
            if (EndDate.HasValue && date > EndDate.Value) yield break;
            if (date >= from) yield return date;
        }
    }

    private DateOnly NthOccurrence(int n) => Unit switch
    {
        CadenceUnit.Day   => StartDate.AddDays(n * Multiplier),
        CadenceUnit.Week  => StartDate.AddDays(n * Multiplier * 7),
        CadenceUnit.Month => StartDate.AddMonths(n * Multiplier),  // .NET clamps to month end
        CadenceUnit.Year  => StartDate.AddYears(n * Multiplier),
        _ => throw new InvalidOperationException($"Unknown cadence unit: {Unit}")
    };
}
```

### StepTemplate (child entity)

```csharp
namespace HomeMaintenance.Domain.JobDefinitions;

public sealed class StepTemplate
{
    public string Id { get; }
    public int Order { get; internal set; }
    public string Description { get; private set; }

    private StepTemplate(string id, int order, string description)
    {
        Id = id;
        Order = order;
        Description = description;
    }

    public static StepTemplate Create(string id, int order, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (description.Trim().Length > 500)
            throw new ArgumentException("Description must be 1..500 characters.", nameof(description));
        return new StepTemplate(id, order, description.Trim());
    }

    public void EditDescription(string newDescription)
    {
        if (string.IsNullOrWhiteSpace(newDescription))
            throw new ArgumentException("Description is required.", nameof(newDescription));
        if (newDescription.Trim().Length > 500)
            throw new ArgumentException("Description must be 1..500 characters.", nameof(newDescription));
        Description = newDescription.Trim();
    }
}
```

### JobDefinition (aggregate root)

```csharp
namespace HomeMaintenance.Domain.JobDefinitions;

public sealed class JobDefinition : Entity
{
    private readonly List<StepTemplate> _stepTemplates = new();

    public OwnerId Owner { get; }
    public string PropertyId { get; }
    public string Name { get; private set; }
    public ScheduleDefinition Schedule { get; private set; }
    public IReadOnlyList<StepTemplate> StepTemplates => _stepTemplates.AsReadOnly();

    private JobDefinition(string id, OwnerId owner, string propertyId, string name, ScheduleDefinition schedule)
        : base(id)
    {
        Owner = owner;
        PropertyId = propertyId;
        Name = name;
        Schedule = schedule;
    }

    public static JobDefinition Create(string id, OwnerId owner, string propertyId, string name,
        ScheduleDefinition schedule, IEnumerable<string> initialStepDescriptions)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (string.IsNullOrWhiteSpace(propertyId))
            throw new ArgumentException("PropertyId is required.", nameof(propertyId));
        ValidateName(name);
        var def = new JobDefinition(id, owner, propertyId, name.Trim(), schedule);
        var order = 0;
        foreach (var desc in initialStepDescriptions)
            def._stepTemplates.Add(StepTemplate.Create(NewTemplateId(), order++, desc));
        return def;
    }

    internal static JobDefinition Hydrate(string id, OwnerId owner, string propertyId,
        string name, ScheduleDefinition schedule, IEnumerable<StepTemplate> templates)
    {
        var def = new JobDefinition(id, owner, propertyId, name, schedule);
        def._stepTemplates.AddRange(templates);
        return def;
    }

    public void Rename(string newName) { ValidateName(newName); Name = newName.Trim(); }
    public void UpdateSchedule(ScheduleDefinition schedule) => Schedule = schedule;

    // Step template mutations --- same outcome pattern as Slice 1 Job
    public StepTemplate AddStepTemplate(string description) { ... }
    public StepMutationOutcome RemoveStepTemplate(string templateId) { ... }
    public ReorderStepsOutcome ReorderStepTemplates(IReadOnlyList<string> orderedIds) { ... }
    public StepMutationOutcome EditStepTemplateDescription(string templateId, string newDescription) { ... }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (name.Trim().Length > 200)
            throw new ArgumentException("Name must be 1..200 characters.", nameof(name));
    }

    private static string NewTemplateId() => Guid.NewGuid().ToString("N");
}
```

### Job (extended from Slice 1)

Add to existing `Job` aggregate:

```csharp
public string? JobDefinitionId { get; private set; }  // null = one-shot

// Extended Create factory
public static Job Create(string id, OwnerId owner, string propertyId, string name,
    DateOnly? dueDate, IEnumerable<string> initialStepDescriptions,
    string? jobDefinitionId = null)  // new optional param

// Extended Hydrate
internal static Job Hydrate(..., string? jobDefinitionId)
```

`JobDefinitionId` is set once at creation and never mutated.

## Application layer

### IJobDefinitionRepository

```csharp
public interface IJobDefinitionRepository
{
    Task<JobDefinition?> GetAsync(string id, OwnerId owner, CancellationToken ct);
    Task<IReadOnlyList<JobDefinition>> ListAsync(OwnerId owner, string? propertyId, CancellationToken ct);
    Task<IReadOnlyList<JobDefinition>> ListAllActiveAsync(CancellationToken ct);  // system/scheduler use
    Task AddAsync(JobDefinition definition, CancellationToken ct);
    Task UpdateAsync(JobDefinition definition, CancellationToken ct);
}
```

### IDateTimeProvider

```csharp
public interface IDateTimeProvider
{
    DateOnly UtcToday { get; }
}
```

### DTOs

```csharp
public sealed record ScheduleDefinitionDto(
    string Unit,          // "Day" | "Week" | "Month" | "Year"
    int Multiplier,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record StepTemplateDto(
    string Id,
    int Order,
    string Description);

public sealed record JobDefinitionDto(
    string Id,
    string PropertyId,
    string Name,
    ScheduleDefinitionDto Schedule,
    IReadOnlyList<StepTemplateDto> StepTemplates);

// JobDto (existing) gains:
public sealed record JobDto(
    string Id,
    string PropertyId,
    string Name,
    DateOnly? DueDate,
    string Status,
    DateTime? CompletedAt,
    IReadOnlyList<StepDto> Steps,
    string? JobDefinitionId);  // NEW --- null for one-shot jobs
```

## Infrastructure layer (MongoDB)

### job_definitions collection

```json
{
  "_id": "string (GUID)",
  "ownerId": "string",
  "propertyId": "string",
  "name": "string",
  "stepTemplates": [
    { "id": "string", "order": 0, "description": "string" }
  ],
  "schedule": {
    "unit": "Month",
    "multiplier": 3,
    "startDate": "2026-06-01",
    "endDate": null
  },
  "createdAt": "ISODate",
  "updatedAt": "ISODate"
}
```

**Indexes**:
- `{ ownerId: 1 }` --- list by owner
- `{ ownerId: 1, propertyId: 1 }` --- filter by property

### jobs collection (existing --- extended)

New field added to existing documents:

```json
{
  "jobDefinitionId": "string | null"
}
```

Existing documents that lack this field behave as `null` (one-shot). No migration script needed --- MongoDB handles missing fields as null.

**New index**:
- `{ jobDefinitionId: 1 }` sparse --- only indexes documents where field exists, supporting:
  - "Has this occurrence already been generated?" query
  - "Latest generated job due date for this definition?" query

### DateOnly BSON serialisation

`DateOnly` is stored as `"YYYY-MM-DD"` strings (same convention as Slice 1's `DueDate`). The existing `DateOnly` BSON serialiser registered in Slice 1 covers all new uses.
