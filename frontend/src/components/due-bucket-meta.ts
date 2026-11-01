import type { DueBucket } from "@/lib/due-jobs";

export interface DueBucketMeta {
  key: DueBucket;
  title: string;
  pillClass: string;
}

/**
 * Single source of truth for each due-job bucket's display order, title,
 * and pill color - lib/due-jobs.ts owns the bucketing logic (pure, no
 * UI), this owns how each bucket is presented. Adding/renaming a bucket
 * only means editing this one array.
 */
export const DUE_BUCKET_META: readonly DueBucketMeta[] = [
  { key: "overdue", title: "Overdue", pillClass: "bg-red-100 text-red-800" },
  {
    key: "dueThisWeek",
    title: "Due this week",
    pillClass: "bg-yellow-100 text-yellow-800",
  },
  {
    key: "comingUp",
    title: "Coming up this month",
    pillClass: "bg-gray-100 text-gray-800",
  },
];
