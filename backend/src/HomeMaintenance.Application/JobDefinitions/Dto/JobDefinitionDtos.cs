namespace HomeMaintenance.Application.JobDefinitions.Dto;

public sealed record ScheduleDefinitionDto(
    string Unit,
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
