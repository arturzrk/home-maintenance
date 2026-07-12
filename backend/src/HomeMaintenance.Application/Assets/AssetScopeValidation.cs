using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Application.Assets;

/// <summary>
/// Shared FR-06/FR-08 check for scoping new work (jobs, definitions) to
/// an asset: the asset must exist for the caller, belong to the same
/// property, and not be obsolete. Returns null when the scope is valid.
/// </summary>
public static class AssetScopeValidation
{
    public static async Task<Error?> CheckAsync(
        IAssetRepository assets,
        OwnerId owner,
        string propertyId,
        string assetId,
        CancellationToken ct)
    {
        var asset = await assets.GetAsync(assetId, owner, ct);
        if (asset is null)
            return new NotFoundError("Asset", assetId);
        if (asset.PropertyId != propertyId)
            return new BusinessRuleError(
                "asset_property_mismatch",
                "The asset belongs to a different property.");
        if (asset.IsObsolete)
            return new BusinessRuleError(
                "asset_obsolete",
                "An obsolete asset cannot be assigned to new work.");
        return null;
    }
}
