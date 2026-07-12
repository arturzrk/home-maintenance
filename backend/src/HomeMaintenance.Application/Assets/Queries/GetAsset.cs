using HomeMaintenance.Application.Assets.Dto;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;

namespace HomeMaintenance.Application.Assets.Queries;

public sealed record GetAssetQuery(string Id);

public sealed class GetAssetHandler
{
    private readonly IAssetRepository _assets;
    private readonly IIdentityProvider _identity;

    public GetAssetHandler(IAssetRepository assets, IIdentityProvider identity)
    {
        _assets = assets;
        _identity = identity;
    }

    public async Task<Result<AssetDto>> Handle(
        GetAssetQuery query,
        CancellationToken ct = default)
    {
        var asset = await _assets.GetAsync(query.Id, _identity.CurrentOwner, ct);
        return asset is null
            ? Result<AssetDto>.Failure(new NotFoundError("Asset", query.Id))
            : Result<AssetDto>.Success(asset.ToDto());
    }
}
