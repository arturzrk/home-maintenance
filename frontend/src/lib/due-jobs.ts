import { daysUntilDue, formatDueLabel } from "@/lib/date";
import type { JobSummary, Property } from "@/lib/api-client";

export type DueBucket = "overdue" | "dueThisWeek" | "comingUp";

export interface DueJobItem {
  job: JobSummary;
  propertyName: string;
  daysUntilDue: number;
  dueLabel: string;
  bucket: DueBucket;
}

export interface DueJobBuckets {
  overdue: DueJobItem[];
  dueThisWeek: DueJobItem[];
  comingUp: DueJobItem[];
}

const WEEK_WINDOW_DAYS = 7;
const MONTH_WINDOW_DAYS = 30;

function classifyBucket(days: number): DueBucket | null {
  if (days < 0) return "overdue";
  if (days <= WEEK_WINDOW_DAYS) return "dueThisWeek";
  if (days <= MONTH_WINDOW_DAYS) return "comingUp";
  return null;
}

function toDueJobItem(
  job: JobSummary,
  propertyNamesById: Map<string, string>,
  today: Date,
): DueJobItem | null {
  if (job.status !== "Active" || job.dueDate === null) return null;

  const days = daysUntilDue(today, job.dueDate);
  const bucket = classifyBucket(days);
  if (bucket === null) return null;

  return {
    job,
    propertyName: propertyNamesById.get(job.propertyId) ?? "Unknown property",
    daysUntilDue: days,
    dueLabel: formatDueLabel(days),
    bucket,
  };
}

function compareDueJobItems(a: DueJobItem, b: DueJobItem): number {
  if (a.daysUntilDue !== b.daysUntilDue) return a.daysUntilDue - b.daysUntilDue;
  return a.job.name.localeCompare(b.job.name) || a.job.id.localeCompare(b.job.id);
}

/**
 * Groups active jobs across all properties into Overdue / due-this-week
 * / coming-up-this-month buckets. Jobs beyond the 30-day window, with no
 * dueDate, or not Active are excluded entirely. Filters on its own
 * rather than trusting the caller already filtered, so it's correct
 * given an arbitrary JobSummary[].
 */
export function groupJobsByDueDate(
  jobs: JobSummary[],
  properties: Property[],
  today: Date,
): DueJobBuckets {
  const propertyNamesById = new Map(properties.map((p) => [p.id, p.name]));
  const items = jobs
    .map((job) => toDueJobItem(job, propertyNamesById, today))
    .filter((item): item is DueJobItem => item !== null)
    .sort(compareDueJobItems);

  return {
    overdue: items.filter((i) => i.bucket === "overdue"),
    dueThisWeek: items.filter((i) => i.bucket === "dueThisWeek"),
    comingUp: items.filter((i) => i.bucket === "comingUp"),
  };
}
