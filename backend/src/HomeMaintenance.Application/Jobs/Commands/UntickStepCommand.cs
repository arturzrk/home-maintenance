namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record UntickStepCommand(string JobId, string StepId);
