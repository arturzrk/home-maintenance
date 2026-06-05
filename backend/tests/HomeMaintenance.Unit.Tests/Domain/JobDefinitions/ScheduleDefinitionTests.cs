using HomeMaintenance.Domain.JobDefinitions;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Domain.JobDefinitions;

public sealed class ScheduleDefinitionTests
{
    [Fact]
    public void DailyEvery1_TodayToPlus7_Returns8Occurrences()
    {
        var start = new DateOnly(2026, 1, 1);
        var sched = new ScheduleDefinition(CadenceUnit.Day, 1, start);

        var result = sched.OccurrencesInRange(start, start.AddDays(7)).ToList();

        result.Count.ShouldBe(8);
        result[0].ShouldBe(start);
        result[7].ShouldBe(start.AddDays(7));
    }

    [Fact]
    public void WeeklyEvery2_StartToPlus4Weeks_Returns3Occurrences()
    {
        var start = new DateOnly(2026, 1, 1);
        var sched = new ScheduleDefinition(CadenceUnit.Week, 2, start);

        // occurrences: Jan 1, Jan 15, Jan 29 (Jan 1 + 28 days = Jan 29 <= Jan 29)
        var result = sched.OccurrencesInRange(start, start.AddDays(28)).ToList();

        result.Count.ShouldBe(3);
    }

    [Fact]
    public void Monthly_MultiplierGt1_Jan1Plus3Months()
    {
        var start = new DateOnly(2026, 1, 1);
        var sched = new ScheduleDefinition(CadenceUnit.Month, 3, start);

        var result = sched.OccurrencesInRange(start, new DateOnly(2026, 12, 31)).ToList();

        result.ShouldContain(new DateOnly(2026, 1, 1));
        result.ShouldContain(new DateOnly(2026, 4, 1));
        result.ShouldContain(new DateOnly(2026, 7, 1));
        result.ShouldContain(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void Monthly_Jan31_NextIsLastDayOfFeb()
    {
        var start = new DateOnly(2026, 1, 31);
        var sched = new ScheduleDefinition(CadenceUnit.Month, 1, start);

        var result = sched.OccurrencesInRange(start, start.AddMonths(1)).ToList();

        result.ShouldContain(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void Yearly_Feb29_Plus12Months_ClampsToFeb28()
    {
        // 2024 is a leap year; 2025 is not
        var start = new DateOnly(2024, 2, 29);
        var sched = new ScheduleDefinition(CadenceUnit.Year, 1, start);

        var result = sched.OccurrencesInRange(start, new DateOnly(2025, 12, 31)).ToList();

        result.ShouldContain(new DateOnly(2024, 2, 29));
        result.ShouldContain(new DateOnly(2025, 2, 28));
    }

    [Fact]
    public void EndDateCutoff_StopsBeforeEndDate()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 3, 1);
        var sched = new ScheduleDefinition(CadenceUnit.Month, 1, start, end);

        var result = sched.OccurrencesInRange(start, new DateOnly(2026, 12, 31)).ToList();

        result.ShouldContain(new DateOnly(2026, 1, 1));
        result.ShouldContain(new DateOnly(2026, 2, 1));
        result.ShouldContain(new DateOnly(2026, 3, 1));
        result.ShouldNotContain(new DateOnly(2026, 4, 1));
    }

    [Fact]
    public void StartDateInPast_OnlyFromForward()
    {
        var start = new DateOnly(2026, 1, 1);
        var from = new DateOnly(2026, 3, 1);
        var sched = new ScheduleDefinition(CadenceUnit.Month, 1, start);

        var result = sched.OccurrencesInRange(from, new DateOnly(2026, 4, 30)).ToList();

        result.ShouldNotContain(new DateOnly(2026, 1, 1));
        result.ShouldNotContain(new DateOnly(2026, 2, 1));
        result.ShouldContain(new DateOnly(2026, 3, 1));
        result.ShouldContain(new DateOnly(2026, 4, 1));
    }

    [Fact]
    public void EmptyRange_FromGtTo_ReturnsEmpty()
    {
        var start = new DateOnly(2026, 1, 1);
        var sched = new ScheduleDefinition(CadenceUnit.Day, 1, start);

        var result = sched.OccurrencesInRange(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)).ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public void HorizonBoundary_InclusiveOfToDate()
    {
        var start = new DateOnly(2026, 1, 1);
        var sched = new ScheduleDefinition(CadenceUnit.Month, 1, start);
        var to = new DateOnly(2026, 3, 1);

        var result = sched.OccurrencesInRange(start, to).ToList();

        result.ShouldContain(to);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_MultiplierZero_Throws(int invalid)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new ScheduleDefinition(CadenceUnit.Month, invalid, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Constructor_EndDateBeforeStart_Throws()
    {
        var start = new DateOnly(2026, 6, 1);
        Should.Throw<ArgumentException>(() =>
            new ScheduleDefinition(CadenceUnit.Month, 1, start, start));

        Should.Throw<ArgumentException>(() =>
            new ScheduleDefinition(CadenceUnit.Month, 1, start, start.AddDays(-1)));
    }
}
