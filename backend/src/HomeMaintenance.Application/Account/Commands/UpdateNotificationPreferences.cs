using HomeMaintenance.Application.Account.Dto;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;

namespace HomeMaintenance.Application.Account.Commands;

public sealed record UpdateNotificationPreferencesCommand(bool RemindersEnabled);

public sealed class UpdateNotificationPreferencesHandler
{
    private readonly IOwnerProfileRepository _profiles;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public UpdateNotificationPreferencesHandler(
        IOwnerProfileRepository profiles,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _profiles = profiles;
        _identity = identity;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<NotificationPreferencesDto>> Handle(
        UpdateNotificationPreferencesCommand cmd,
        CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;

        var existing = await _profiles.GetAsync(owner, ct);
        if (existing is null)
        {
            // Shouldn't happen - the auth-pipeline sync runs on every
            // authenticated request, including this one, before the
            // handler is reached. Defensive rather than reachable in
            // normal operation.
            return Result<NotificationPreferencesDto>.Failure(
                new NotFoundError("OwnerProfile", owner.Value));
        }

        await _profiles.UpdateRemindersEnabledAsync(owner, cmd.RemindersEnabled, ct);

        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.NotificationPreferencesUpdated,
            owner.Value,
            $"owner-profile:{owner.Value}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            new Dictionary<string, object?> { ["remindersEnabled"] = cmd.RemindersEnabled }), ct);

        return Result<NotificationPreferencesDto>.Success(
            new NotificationPreferencesDto(existing.Email, cmd.RemindersEnabled));
    }
}
