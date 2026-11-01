namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record CreateJobCommand(
    string PropertyId,
    string Name,
    DateOnly? DueDate,
    IReadOnlyList<string> StepDescriptions,
    string? AssetId = null);
