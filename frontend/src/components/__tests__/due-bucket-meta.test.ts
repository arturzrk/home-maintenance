import { DUE_BUCKET_META } from "@/components/due-bucket-meta";

describe("DUE_BUCKET_META", () => {
  it("hasExactlyTheThreeExpectedBuckets_InDisplayOrder", () => {
    expect(DUE_BUCKET_META.map((b) => b.key)).toEqual([
      "overdue",
      "dueThisWeek",
      "comingUp",
    ]);
  });

  it("everyEntry_HasATitleAndColorClasses", () => {
    for (const entry of DUE_BUCKET_META) {
      expect(entry.title.length).toBeGreaterThan(0);
      expect(entry.textClass.length).toBeGreaterThan(0);
      expect(entry.iconBg.length).toBeGreaterThan(0);
      expect(entry.iconColor.length).toBeGreaterThan(0);
    }
  });

  it("keys_AreUnique", () => {
    const keys = DUE_BUCKET_META.map((b) => b.key);
    expect(new Set(keys).size).toBe(keys.length);
  });
});
