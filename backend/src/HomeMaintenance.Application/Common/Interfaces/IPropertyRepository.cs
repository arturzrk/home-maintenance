using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Properties;

namespace HomeMaintenance.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for the Property aggregate. Every method is
/// scoped to a single <see cref="OwnerId"/>; cross-owner access returns
/// null from <see cref="GetAsync"/>, which handlers translate to
/// <see cref="NotFoundError"/> (no leak; see contracts/README.md).
/// </summary>
public interface IPropertyRepository
{
    Task<Property?> GetAsync(string id, OwnerId owner, CancellationToken ct = default);
    Task<IReadOnlyList<Property>> ListAsync(OwnerId owner, CancellationToken ct = default);
    Task AddAsync(Property property, CancellationToken ct = default);
    Task UpdateAsync(Property property, CancellationToken ct = default);
}
