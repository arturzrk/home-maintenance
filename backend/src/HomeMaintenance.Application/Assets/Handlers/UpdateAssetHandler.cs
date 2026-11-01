using HomeMaintenance.Application.Assets.Commands;
using HomeMaintenance.Application.Assets.Dto;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Assets;
using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Application.Assets.Handlers;

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
        if (IsEmptyRequest(cmd))
            return Result<AssetDto>.Failure(
                new ValidationError("request", "Provide at least one change to apply."));

        var owner = _identity.CurrentOwner;

        var asset = await _assets.GetAsync(cmd.Id, owner, ct);
        if (asset is null)
            return Result<AssetDto>.Failure(new NotFoundError("Asset", cmd.Id));

        var payload = new Dictionary<string, object?>();
        var error = ApplyRename(cmd, asset, payload)
            ?? ApplyCategory(cmd, asset, payload)
            ?? ApplyNotes(cmd, asset, payload);
        if (error is not null)
            return Result<AssetDto>.Failure(error);

        ApplyObsolete(cmd, asset, payload);

        await _assets.UpdateAsync(asset, ct);
        await EmitAudit(asset.Id, owner, payload, ct);

        return Result<AssetDto>.Success(asset.ToDto());
    }

    private static Error? ApplyRename(UpdateAssetCommand cmd, Asset asset, Dictionary<string, object?> payload)
    {
        if (cmd.Name is null)
            return null;

        var error = AsValidationError(() => asset.Rename(cmd.Name), "name");
        if (error is not null)
            return error;

        payload["name"] = asset.Name;
        return null;
    }

    private static Error? ApplyCategory(UpdateAssetCommand cmd, Asset asset, Dictionary<string, object?> payload)
    {
        if (cmd.Category is null)
            return null;

        var error = AsValidationError(() => asset.SetCategory(cmd.Category), "category");
        if (error is not null)
            return error;

        payload["category"] = asset.Category;
        return null;
    }

    private static Error? ApplyNotes(UpdateAssetCommand cmd, Asset asset, Dictionary<string, object?> payload)
    {
        if (cmd.Notes is null)
            return null;

        var error = AsValidationError(() => asset.SetNotes(cmd.Notes), "notes");
        if (error is not null)
            return error;

        payload["notes_changed"] = true;
        return null;
    }

    private static void ApplyObsolete(UpdateAssetCommand cmd, Asset asset, Dictionary<string, object?> payload)
    {
        if (cmd.IsObsolete is null)
            return;

        asset.SetObsolete(cmd.IsObsolete.Value);
        payload["isObsolete"] = asset.IsObsolete;
    }

    private Task EmitAudit(string assetId, OwnerId owner, Dictionary<string, object?> payload, CancellationToken ct)
        => _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.AssetUpdated,
            owner.Value,
            $"asset:{assetId}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            payload), ct);

    private static bool IsEmptyRequest(UpdateAssetCommand cmd)
        => cmd.Name is null && cmd.Category is null && cmd.Notes is null && cmd.IsObsolete is null;

    /// <summary>Runs <paramref name="mutate"/>, translating a domain ArgumentException into a ValidationError.</summary>
    private static Error? AsValidationError(Action mutate, string fallbackField)
    {
        try { mutate(); return null; }
        catch (ArgumentException ex) { return new ValidationError(ex.ParamName ?? fallbackField, ex.Message); }
    }
}
