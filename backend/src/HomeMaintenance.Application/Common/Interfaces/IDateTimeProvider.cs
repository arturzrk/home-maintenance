namespace HomeMaintenance.Application.Common.Interfaces;

public interface IDateTimeProvider
{
    DateOnly UtcToday { get; }
}
