using HomeMaintenance.Application.JobDefinitions.Dto;

namespace HomeMaintenance.Application.JobDefinitions.Commands;

public sealed record CreateJobDefinitionCommand(
    string PropertyId,
    string Name,
    ScheduleDefinitionDto Schedule,
    IReadOnlyList<string> StepDescriptions,
    string? AssetId = null);
