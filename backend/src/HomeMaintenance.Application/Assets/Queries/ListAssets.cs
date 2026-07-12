using HomeMaintenance.Application.Assets.Dto;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;

namespace HomeMaintenance.Application.Assets.Queries;

public sealed record ListAssetsQuery(string PropertyId);

public sealed class ListAssetsHandler
{
    private readonly IAssetRepository _assets;
    private readonly IPropertyRepository _properties;
    private readonly IIdentityProvider _identity;

    public ListAssetsHandler(
        IAssetRepository assets,
        IPropertyRepository properties,
        IIdentityProvider identity)
    {
        _assets = assets;
        _properties = properties;
        _identity = identity;
    }

    public async Task<Result<IReadOnlyList<AssetDto>>> Handle(
        ListAssetsQuery query,
        CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;

        var property = await _properties.GetAsync(query.PropertyId, owner, ct);
        if (property is null)
            return Result<IReadOnlyList<AssetDto>>.Failure(new NotFoundError("Property", query.PropertyId));

        var assets = await _assets.ListByPropertyAsync(query.PropertyId, owner, ct);
        return Result<IReadOnlyList<AssetDto>>.Success(
            assets.Select(a => a.ToDto()).ToList());
    }
}
