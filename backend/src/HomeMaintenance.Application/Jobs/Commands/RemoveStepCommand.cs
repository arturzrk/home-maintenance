namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record RemoveStepCommand(string JobId, string StepId);
