using HomeMaintenance.Domain.Common;

namespace HomeMaintenance.Application.Common.Interfaces;

/// <summary>
/// Generic repository interface that defines the persistence contract
/// used by Application layer use cases. Implementations live in Infrastructure.
/// </summary>
/// <typeparam name="TEntity">A domain entity derived from <see cref="Entity"/>.</typeparam>
public interface IRepository<TEntity> where TEntity : Entity
{
    Task<TEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
