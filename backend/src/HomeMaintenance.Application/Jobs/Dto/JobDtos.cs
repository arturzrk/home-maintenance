using System.ComponentModel.DataAnnotations;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Dto;

public sealed record StepDto(
    string Id,
    int Order,
    string Description,
    bool IsCompleted,
    DateTime? CompletedAt);

public sealed record JobSummaryDto(
    string Id,
    string PropertyId,
    string Name,
    DateOnly? DueDate,
    JobStatus Status,
    DateTime? CompletedAt,
    int StepCount,
    int CompletedStepCount,
    string? JobDefinitionId = null);

public sealed record JobDetailDto(
    string Id,
    string PropertyId,
    string Name,
    DateOnly? DueDate,
    JobStatus Status,
    DateTime? CompletedAt,
    IReadOnlyList<StepDto> Steps,
    string? JobDefinitionId = null);

public sealed record JobListDto(IReadOnlyList<JobSummaryDto> Jobs);

public sealed record CreateJobStepRequest(
    [property: Required]
    [property: StringLength(500, MinimumLength = 1)]
    string Description);

public sealed record CreateJobRequest(
    [property: Required]
    string PropertyId,
    [property: Required]
    [property: StringLength(200, MinimumLength = 1)]
    string Name,
    DateOnly? DueDate,
    [property: Required]
    IReadOnlyList<CreateJobStepRequest> Steps);

public sealed record AddStepRequest(
    [property: Required]
    [property: StringLength(500, MinimumLength = 1)]
    string Description);

public sealed record EditStepDescriptionRequest(
    [property: Required]
    [property: StringLength(500, MinimumLength = 1)]
    string Description);

public sealed record ReorderStepsRequest(
    [property: Required]
    [property: MinLength(1)]
    IReadOnlyList<string> OrderedStepIds);

/// <summary>
/// PATCH /api/jobs/{id} request body. Both fields are optional; the
/// server treats an explicit "dueDate": null as "clear the due date"
/// and an omitted "dueDate" as "leave unchanged".
/// </summary>
public sealed record UpdateJobRequest(
    [property: StringLength(200, MinimumLength = 1)]
    string? Name,
    DateOnly? DueDate);
