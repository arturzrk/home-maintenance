namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record EditStepDescriptionCommand(string JobId, string StepId, string Description);
