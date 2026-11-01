namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record AddStepCommand(string JobId, string Description);
