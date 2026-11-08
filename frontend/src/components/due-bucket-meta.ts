import type { DueBucket } from "@/lib/due-jobs";

export interface DueBucketMeta {
  key: DueBucket;
  title: string;
  /** Color for the job-count number and each card's due-label text. */
  textClass: string;
  /** Background for the icon badge next to the section title. */
  iconBg: string;
  /** Color for the icon itself. */
  iconColor: string;
}

/**
 * Single source of truth for each due-job bucket's display order, title,
 * and color treatment - lib/due-jobs.ts owns the bucketing logic (pure,
 * no UI), this owns how each bucket is presented. Adding/renaming a
 * bucket only means editing this one array.
 */
export const DUE_BUCKET_META: readonly DueBucketMeta[] = [
  {
    key: "overdue",
    title: "Overdue",
    textClass: "text-rose-500",
    iconBg: "bg-rose-50",
    iconColor: "text-rose-500",
  },
  {
    key: "dueThisWeek",
    title: "Due this week",
    textClass: "text-amber-600",
    iconBg: "bg-amber-50",
    iconColor: "text-amber-500",
  },
  {
    key: "comingUp",
    title: "Coming up this month",
    textClass: "text-gray-400",
    iconBg: "bg-gray-100",
    iconColor: "text-gray-400",
  },
];
