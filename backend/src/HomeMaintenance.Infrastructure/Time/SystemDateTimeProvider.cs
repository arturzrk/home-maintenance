using HomeMaintenance.Application.Common.Interfaces;

namespace HomeMaintenance.Infrastructure.Time;

/// <summary>
/// Production <see cref="IDateTimeProvider"/> backed by the system clock,
/// expressed in UTC so scheduled-job generation is independent of the
/// host's local time zone.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);
}
