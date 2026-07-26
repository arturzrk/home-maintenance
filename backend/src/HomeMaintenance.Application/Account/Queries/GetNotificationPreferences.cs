using HomeMaintenance.Application.Account.Dto;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;

namespace HomeMaintenance.Application.Account.Queries;

public sealed record GetNotificationPreferencesQuery;

public sealed class GetNotificationPreferencesHandler
{
    private readonly IOwnerProfileRepository _profiles;
    private readonly IIdentityProvider _identity;

    public GetNotificationPreferencesHandler(IOwnerProfileRepository profiles, IIdentityProvider identity)
    {
        _profiles = profiles;
        _identity = identity;
    }

    public async Task<Result<NotificationPreferencesDto>> Handle(
        GetNotificationPreferencesQuery _,
        CancellationToken ct = default)
    {
        var profile = await _profiles.GetAsync(_identity.CurrentOwner, ct);

        // No profile yet (email capture hasn't run for this owner) is not
        // an error - reminders default to enabled per FR-06.
        var dto = profile is null
            ? new NotificationPreferencesDto(Email: null, RemindersEnabled: true)
            : new NotificationPreferencesDto(profile.Email, profile.RemindersEnabled);

        return Result<NotificationPreferencesDto>.Success(dto);
    }
}
