import { DueJobSection } from "@/components/due-job-section";
import type { DueJobBuckets } from "@/lib/due-jobs";

export function DueJobsOverview({ buckets }: { buckets: DueJobBuckets }) {
  const isAllCaughtUp =
    buckets.overdue.length === 0 &&
    buckets.dueThisWeek.length === 0 &&
    buckets.comingUp.length === 0;

  if (isAllCaughtUp) {
    return (
      <div className="rounded-md border border-gray-200 bg-white px-4 py-6 text-center shadow-sm">
        <p className="text-base font-medium text-gray-900">
          You&apos;re all caught up!
        </p>
        <p className="mt-1 text-sm text-gray-500">
          No jobs are overdue or due in the next 30 days.
        </p>
      </div>
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
