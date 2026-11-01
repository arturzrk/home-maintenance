import { groupJobsByDueDate } from "@/lib/due-jobs";
import type { JobSummary, Property } from "@/lib/api-client";

const today = new Date(2026, 10, 1); // 2026-11-01

const property1: Property = { id: "prop-1", name: "Lakeview Cottage" };
const property2: Property = { id: "prop-2", name: "City Flat" };

function job(overrides: Partial<JobSummary>): JobSummary {
  return {
    id: "job-1",
    propertyId: property1.id,
    name: "Service boiler",
    dueDate: "2026-11-01",
    status: "Active",
    completedAt: null,
    stepCount: 1,
    completedStepCount: 0,
    jobDefinitionId: null,
    assetId: null,
    ...overrides,
  };
}

describe("groupJobsByDueDate", () => {
  it("emptyInput_ReturnsThreeEmptyArrays", () => {
    const result = groupJobsByDueDate([], [property1], today);
    expect(result).toEqual({ overdue: [], dueThisWeek: [], comingUp: [] });
  });

  it("dueYesterday_IsOverdue_NotDueThisWeek", () => {
    const j = job({ id: "j1", dueDate: "2026-10-31" });
    const result = groupJobsByDueDate([j], [property1], today);
    expect(result.overdue.map((i) => i.job.id)).toEqual(["j1"]);
    expect(result.dueThisWeek).toEqual([]);
  });

  it("dueToday_IsDueThisWeek_NotOverdue", () => {
    const j = job({ id: "j1", dueDate: "2026-11-01" });
    const result = groupJobsByDueDate([j], [property1], today);
    expect(result.dueThisWeek.map((i) => i.job.id)).toEqual(["j1"]);
    expect(result.overdue).toEqual([]);
  });

  it("duePlus7_IsDueThisWeek_NotComingUp", () => {
    const j = job({ id: "j1", dueDate: "2026-11-08" });
    const result = groupJobsByDueDate([j], [property1], today);
    expect(result.dueThisWeek.map((i) => i.job.id)).toEqual(["j1"]);
    expect(result.comingUp).toEqual([]);
  });

  it("duePlus8_IsComingUp_NotDueThisWeek", () => {
    const j = job({ id: "j1", dueDate: "2026-11-09" });
    const result = groupJobsByDueDate([j], [property1], today);
    expect(result.comingUp.map((i) => i.job.id)).toEqual(["j1"]);
    expect(result.dueThisWeek).toEqual([]);
  });

  it("duePlus30_IsComingUp", () => {
    const j = job({ id: "j1", dueDate: "2026-12-01" });
    const result = groupJobsByDueDate([j], [property1], today);
    expect(result.comingUp.map((i) => i.job.id)).toEqual(["j1"]);
  });

  it("duePlus31_IsInNoBucket", () => {
    const j = job({ id: "j1", dueDate: "2026-12-02" });
    const result = groupJobsByDueDate([j], [property1], today);
    const total =
      result.overdue.length + result.dueThisWeek.length + result.comingUp.length;
    expect(total).toBe(0);
  });

  it("completedStatus_IsExcluded_EvenWithinWindow", () => {
    const j = job({ id: "j1", dueDate: "2026-11-02", status: "Completed" });
    const result = groupJobsByDueDate([j], [property1], today);
    const total =
      result.overdue.length + result.dueThisWeek.length + result.comingUp.length;
    expect(total).toBe(0);
  });

  it("nullDueDate_IsExcluded", () => {
    const j = job({ id: "j1", dueDate: null });
    const result = groupJobsByDueDate([j], [property1], today);
    const total =
      result.overdue.length + result.dueThisWeek.length + result.comingUp.length;
    expect(total).toBe(0);
  });

  it("knownProperty_ResolvesPropertyName", () => {
    const j = job({ id: "j1", propertyId: property1.id, dueDate: "2026-11-01" });
    const result = groupJobsByDueDate([j], [property1], today);
    expect(result.dueThisWeek[0].propertyName).toBe("Lakeview Cottage");
  });

  it("unknownProperty_FallsBackToUnknownLabel", () => {
    const j = job({ id: "j1", propertyId: "missing-prop", dueDate: "2026-11-01" });
    const result = groupJobsByDueDate([j], [property1], today);
    expect(result.dueThisWeek[0].propertyName).toBe("Unknown property");
  });

  it("multipleProperties_EachJobGetsItsOwnPropertyName", () => {
    const j1 = job({ id: "j1", propertyId: property1.id, dueDate: "2026-11-01" });
    const j2 = job({ id: "j2", propertyId: property2.id, dueDate: "2026-11-02" });
    const result = groupJobsByDueDate([j1, j2], [property1, property2], today);
    const byId = Object.fromEntries(
      result.dueThisWeek.map((i) => [i.job.id, i.propertyName]),
    );
    expect(byId).toEqual({ j1: "Lakeview Cottage", j2: "City Flat" });
  });

  it("overdueBucket_SortedMostOverdueFirst", () => {
    const j1 = job({ id: "j1", dueDate: "2026-10-31" }); // 1 day overdue
    const j2 = job({ id: "j2", dueDate: "2026-10-20" }); // 12 days overdue
    const result = groupJobsByDueDate([j1, j2], [property1], today);
    expect(result.overdue.map((i) => i.job.id)).toEqual(["j2", "j1"]);
  });

  it("dueThisWeekBucket_SortedSoonestFirst", () => {
    const j1 = job({ id: "j1", dueDate: "2026-11-05" });
    const j2 = job({ id: "j2", dueDate: "2026-11-02" });
    const result = groupJobsByDueDate([j1, j2], [property1], today);
    expect(result.dueThisWeek.map((i) => i.job.id)).toEqual(["j2", "j1"]);
  });

  it("sameDueDate_TieBreaksByName", () => {
    const j1 = job({ id: "j2", name: "Zebra task", dueDate: "2026-11-01" });
    const j2 = job({ id: "j1", name: "Alpha task", dueDate: "2026-11-01" });
    const result = groupJobsByDueDate([j1, j2], [property1], today);
    expect(result.dueThisWeek.map((i) => i.job.name)).toEqual([
      "Alpha task",
      "Zebra task",
    ]);
  });

  it("sameDueDateAndName_TieBreaksById", () => {
    const j1 = job({ id: "job-b", name: "Same name", dueDate: "2026-11-01" });
    const j2 = job({ id: "job-a", name: "Same name", dueDate: "2026-11-01" });
    const result = groupJobsByDueDate([j1, j2], [property1], today);
    expect(result.dueThisWeek.map((i) => i.job.id)).toEqual(["job-a", "job-b"]);
  });

  it("item_CarriesDueLabelConsistentWithDaysUntilDue", () => {
    const j = job({ id: "j1", dueDate: "2026-10-30" }); // 2 days overdue
    const result = groupJobsByDueDate([j], [property1], today);
    expect(result.overdue[0].daysUntilDue).toBe(-2);
    expect(result.overdue[0].dueLabel).toBe("Overdue by 2 days");
  });
});
