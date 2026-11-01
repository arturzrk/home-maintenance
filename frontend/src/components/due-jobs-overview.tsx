import { DueJobSection } from "@/components/due-job-section";
import type { DueJobBuckets } from "@/lib/due-jobs";

export function DueJobsOverview({ buckets }: { buckets: DueJobBuckets }) {
  const isAllCaughtUp =
    buckets.overdue.length === 0 &&
    buckets.dueThisWeek.length === 0 &&
    buckets.comingUp.length === 0;

  if (isAllCaughtUp) {
    return (
      <p className="text-sm text-gray-500">
        You&apos;re all caught up! No jobs are overdue or due in the next 30
        days.
      </p>
    );
  }

  return (
    <div className="space-y-4">
      <DueJobSection title="Overdue" items={buckets.overdue} />
      <DueJobSection title="Due this week" items={buckets.dueThisWeek} />
      <DueJobSection title="Coming up this month" items={buckets.comingUp} />
    </div>
  );
}
