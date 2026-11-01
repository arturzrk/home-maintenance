namespace HomeMaintenance.Application.Assets.Commands;

/// <summary>
/// PATCH semantics: null means "leave unchanged". For Category and Notes
/// an empty/whitespace string clears the field (the domain normalizes it
/// to null), mirroring how the UI clears optional text inputs.
/// </summary>
public sealed record UpdateAssetCommand(
    string Id,
    string? Name = null,
    string? Category = null,
    string? Notes = null,
    bool? IsObsolete = null);
