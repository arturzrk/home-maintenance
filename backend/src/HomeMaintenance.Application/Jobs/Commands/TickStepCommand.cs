namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record TickStepCommand(string JobId, string StepId);
