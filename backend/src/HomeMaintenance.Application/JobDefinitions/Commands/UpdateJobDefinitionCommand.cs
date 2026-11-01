using HomeMaintenance.Application.JobDefinitions.Dto;

namespace HomeMaintenance.Application.JobDefinitions.Commands;

public sealed record UpdateJobDefinitionCommand(
    string Id,
    string? Name = null,
    ScheduleDefinitionDto? Schedule = null,
    IReadOnlyList<string>? AddStepDescriptions = null,
    IReadOnlyList<string>? RemoveStepTemplateIds = null,
    IReadOnlyList<string>? ReorderStepTemplateIds = null,
    IReadOnlyList<StepTemplateEdit>? EditStepTemplates = null);
