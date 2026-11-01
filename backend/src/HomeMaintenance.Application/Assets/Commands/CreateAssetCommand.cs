namespace HomeMaintenance.Application.Assets.Commands;

public sealed record CreateAssetCommand(
    string PropertyId,
    string Name,
    string? Category = null,
    string? Notes = null);
