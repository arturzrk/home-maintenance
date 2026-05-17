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
    int CompletedStepCount);

public sealed record JobDetailDto(
    string Id,
    string PropertyId,
    string Name,
    DateOnly? DueDate,
    JobStatus Status,
    DateTime? CompletedAt,
    IReadOnlyList<StepDto> Steps);

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
