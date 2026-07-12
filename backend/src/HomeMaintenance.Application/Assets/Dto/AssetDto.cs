using HomeMaintenance.Domain.Assets;

namespace HomeMaintenance.Application.Assets.Dto;

public sealed record AssetDto(
    string Id,
    string PropertyId,
    string Name,
    string? Category,
    string? Notes,
    bool IsObsolete);

public static class AssetDtoMapper
{
    public static AssetDto ToDto(this Asset asset)
        => new(asset.Id, asset.PropertyId, asset.Name, asset.Category, asset.Notes, asset.IsObsolete);
}
