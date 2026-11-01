import {
  daysUntilDue,
  formatDueLabel,
  parseIsoDateOnly,
  todayEpochDay,
} from "@/lib/date";

describe("parseIsoDateOnly", () => {
  it("knownDates_DifferByExpectedDayCount", () => {
    const jan1 = parseIsoDateOnly("2026-01-01");
    const jan8 = parseIsoDateOnly("2026-01-08");
    expect(jan8 - jan1).toBe(7);
  });
});

describe("todayEpochDay", () => {
  it("ignoresTimeOfDay_SameCalendarDate", () => {
    const earlyMorning = new Date(2026, 10, 1, 0, 1);
    const lateNight = new Date(2026, 10, 1, 23, 59);
    expect(todayEpochDay(earlyMorning)).toBe(todayEpochDay(lateNight));
  });

  it("differentCalendarDates_DifferByOne", () => {
    const today = new Date(2026, 10, 1, 12, 0);
    const tomorrow = new Date(2026, 10, 2, 0, 0);
    expect(todayEpochDay(tomorrow) - todayEpochDay(today)).toBe(1);
  });
});

describe("daysUntilDue", () => {
  const today = new Date(2026, 10, 1);

  it("dueDateEqualsToday_ReturnsZero", () => {
    expect(daysUntilDue(today, "2026-11-01")).toBe(0);
  });

  it("dueDateYesterday_ReturnsNegativeOne", () => {
    expect(daysUntilDue(today, "2026-10-31")).toBe(-1);
  });

  it("dueDatePlus7_ReturnsSeven", () => {
    expect(daysUntilDue(today, "2026-11-08")).toBe(7);
  });

  it("dueDatePlus8_ReturnsEight", () => {
    expect(daysUntilDue(today, "2026-11-09")).toBe(8);
  });

  it("dueDatePlus30_ReturnsThirty", () => {
    expect(daysUntilDue(today, "2026-12-01")).toBe(30);
  });

  it("dueDatePlus31_ReturnsThirtyOne", () => {
    expect(daysUntilDue(today, "2026-12-02")).toBe(31);
  });

  it("crossesMonthBoundary_ReturnsOne", () => {
    expect(daysUntilDue(new Date(2026, 0, 31), "2026-02-01")).toBe(1);
  });

  it("crossesYearBoundary_ReturnsOne", () => {
    expect(daysUntilDue(new Date(2026, 11, 31), "2027-01-01")).toBe(1);
  });
});

describe("formatDueLabel", () => {
  it("negativeOne_ReturnsSingularOverdue", () => {
    expect(formatDueLabel(-1)).toBe("Overdue by 1 day");
  });

  it("negativeThree_ReturnsPluralOverdue", () => {
    expect(formatDueLabel(-3)).toBe("Overdue by 3 days");
  });

  it("zero_ReturnsDueToday", () => {
    expect(formatDueLabel(0)).toBe("Due today");
  });

  it("one_ReturnsSingularDueIn", () => {
    expect(formatDueLabel(1)).toBe("Due in 1 day");
  });

  it("five_ReturnsPluralDueIn", () => {
    expect(formatDueLabel(5)).toBe("Due in 5 days");
  });
});
