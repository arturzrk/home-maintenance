using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Application.Common.Interfaces;

/// <summary>
/// Resolves the <see cref="OwnerId"/> of the caller for the current
/// request scope. Concrete implementations live in Infrastructure
/// (HTTP context, local stub, etc.); use cases depend only on this
/// abstraction.
/// </summary>
public interface IIdentityProvider
{
    OwnerId CurrentOwner { get; }
}
