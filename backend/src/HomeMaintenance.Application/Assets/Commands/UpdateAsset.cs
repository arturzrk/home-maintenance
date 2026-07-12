using HomeMaintenance.Application.Assets.Dto;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;

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

public sealed class UpdateAssetHandler
{
    private readonly IAssetRepository _assets;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public UpdateAssetHandler(
        IAssetRepository assets,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _assets = assets;
        _identity = identity;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<AssetDto>> Handle(
        UpdateAssetCommand cmd,
        CancellationToken ct = default)
    {
        if (cmd.Name is null && cmd.Category is null && cmd.Notes is null && cmd.IsObsolete is null)
        {
            return Result<AssetDto>.Failure(
                new ValidationError("request", "Provide at least one change to apply."));
        }

        var owner = _identity.CurrentOwner;

        var asset = await _assets.GetAsync(cmd.Id, owner, ct);
        if (asset is null)
            return Result<AssetDto>.Failure(new NotFoundError("Asset", cmd.Id));

        var payload = new Dictionary<string, object?>();

        try
        {
            if (cmd.Name is not null)
            {
                asset.Rename(cmd.Name);
                payload["name"] = asset.Name;
            }

            if (cmd.Category is not null)
            {
                asset.SetCategory(cmd.Category);
                payload["category"] = asset.Category;
            }

            if (cmd.Notes is not null)
            {
                asset.SetNotes(cmd.Notes);
                payload["notes_changed"] = true;
            }
        }
        catch (ArgumentException ex)
        {
            return Result<AssetDto>.Failure(new ValidationError(ex.ParamName ?? "request", ex.Message));
        }

        if (cmd.IsObsolete is not null)
        {
            asset.SetObsolete(cmd.IsObsolete.Value);
            payload["isObsolete"] = asset.IsObsolete;
        }

        await _assets.UpdateAsync(asset, ct);

        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.AssetUpdated,
            owner.Value,
            $"asset:{asset.Id}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            payload), ct);

        return Result<AssetDto>.Success(asset.ToDto());
    }
}
