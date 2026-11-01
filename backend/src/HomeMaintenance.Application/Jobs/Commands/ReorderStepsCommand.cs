namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record ReorderStepsCommand(string JobId, IReadOnlyList<string> OrderedStepIds);
