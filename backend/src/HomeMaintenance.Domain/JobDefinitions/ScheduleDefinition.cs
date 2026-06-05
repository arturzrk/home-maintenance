namespace HomeMaintenance.Domain.JobDefinitions;

public sealed record ScheduleDefinition
{
    public CadenceUnit Unit { get; }
    public int Multiplier { get; }
    public DateOnly StartDate { get; }
    public DateOnly? EndDate { get; }

    public ScheduleDefinition(CadenceUnit unit, int multiplier, DateOnly startDate, DateOnly? endDate = null)
    {
        if (multiplier < 1)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be >= 1.");
        if (endDate.HasValue && endDate.Value <= startDate)
            throw new ArgumentException("EndDate must be strictly after StartDate.", nameof(endDate));

        Unit = unit;
        Multiplier = multiplier;
        StartDate = startDate;
        EndDate = endDate;
    }

    public IEnumerable<DateOnly> OccurrencesInRange(DateOnly from, DateOnly to)
    {
        if (from > to) yield break;

        for (var n = 0; n <= 10_000; n++)
        {
            var occ = NthOccurrence(n);
            if (occ > to) yield break;
            if (EndDate.HasValue && occ > EndDate.Value) yield break;
            if (occ >= from) yield return occ;
        }
    }

    private DateOnly NthOccurrence(int n) => Unit switch
    {
        CadenceUnit.Day   => StartDate.AddDays(n * Multiplier),
        CadenceUnit.Week  => StartDate.AddDays(n * Multiplier * 7),
        CadenceUnit.Month => StartDate.AddMonths(n * Multiplier),
        CadenceUnit.Year  => StartDate.AddYears(n * Multiplier),
        _ => throw new InvalidOperationException($"Unknown CadenceUnit: {Unit}"),
    };
}
