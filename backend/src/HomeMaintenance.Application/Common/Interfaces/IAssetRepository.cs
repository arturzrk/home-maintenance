using HomeMaintenance.Domain.Assets;
using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Application.Common.Interfaces;

/// <summary>
/// Persistence port for the Asset aggregate. Every read is owner-scoped;
/// cross-owner lookups return null (translated to 404 by handlers).
/// </summary>
public interface IAssetRepository
{
    Task<Asset?> GetAsync(string id, OwnerId owner, CancellationToken ct = default);

    Task<IReadOnlyList<Asset>> ListByPropertyAsync(string propertyId, OwnerId owner, CancellationToken ct = default);

    Task AddAsync(Asset asset, CancellationToken ct = default);

    Task UpdateAsync(Asset asset, CancellationToken ct = default);
}
