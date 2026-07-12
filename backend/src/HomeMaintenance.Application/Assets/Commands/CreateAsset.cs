using HomeMaintenance.Application.Assets.Dto;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Assets;

namespace HomeMaintenance.Application.Assets.Commands;

public sealed record CreateAssetCommand(
    string PropertyId,
    string Name,
    string? Category = null,
    string? Notes = null);

public sealed class CreateAssetHandler
{
    private readonly IAssetRepository _assets;
    private readonly IPropertyRepository _properties;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public CreateAssetHandler(
        IAssetRepository assets,
        IPropertyRepository properties,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _assets = assets;
        _properties = properties;
        _identity = identity;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<AssetDto>> Handle(
        CreateAssetCommand cmd,
        CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;

        var property = await _properties.GetAsync(cmd.PropertyId, owner, ct);
        if (property is null)
            return Result<AssetDto>.Failure(new NotFoundError("Property", cmd.PropertyId));

        Asset asset;
        try
        {
            asset = Asset.Create(IdFactory.NewId(), owner, cmd.PropertyId, cmd.Name, cmd.Category, cmd.Notes);
        }
        catch (ArgumentException ex)
        {
            return Result<AssetDto>.Failure(new ValidationError(ex.ParamName ?? "name", ex.Message));
        }

        await _assets.AddAsync(asset, ct);

        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.AssetCreated,
            owner.Value,
            $"asset:{asset.Id}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            new Dictionary<string, object?>
            {
                ["name"] = asset.Name,
                ["propertyId"] = asset.PropertyId,
            }), ct);

        return Result<AssetDto>.Success(asset.ToDto());
    }
}
