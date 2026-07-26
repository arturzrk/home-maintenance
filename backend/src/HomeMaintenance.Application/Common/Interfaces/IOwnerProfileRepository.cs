using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Application.Common.Interfaces;

public interface IOwnerProfileRepository
{
    Task<OwnerProfile?> GetAsync(OwnerId owner, CancellationToken ct = default);

    /// <summary>
    /// Creates the owner's profile (reminders enabled by default) if it
    /// doesn't exist yet, or updates the stored email if it changed.
    /// No-op if the stored email already matches. Called from the
    /// auth-pipeline sync on every authenticated request, so this must
    /// avoid an unconditional write.
    /// </summary>
    Task UpsertEmailAsync(OwnerId owner, string email, CancellationToken ct = default);

    Task UpdateRemindersEnabledAsync(OwnerId owner, bool enabled, CancellationToken ct = default);
}
