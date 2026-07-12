using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;

namespace HomeMaintenance.Application.Common.Interfaces;

public interface IJobDefinitionRepository
{
    Task<JobDefinition?> GetAsync(string id, OwnerId owner, CancellationToken ct = default);
    Task<IReadOnlyList<JobDefinition>> ListAsync(OwnerId owner, string? propertyId, string? assetId = null, CancellationToken ct = default);
    Task<IReadOnlyList<JobDefinition>> ListAllActiveAsync(CancellationToken ct = default);
    Task AddAsync(JobDefinition definition, CancellationToken ct = default);
    Task UpdateAsync(JobDefinition definition, CancellationToken ct = default);
}
