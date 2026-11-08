import { DUE_BUCKET_META } from "@/components/due-bucket-meta";
import { DueBucketIcon } from "@/components/due-bucket-icon";
import { DueJobSection } from "@/components/due-job-section";
import type { DueJobBuckets } from "@/lib/due-jobs";

export function DueJobsOverview({ buckets }: { buckets: DueJobBuckets }) {
  const isAllCaughtUp = DUE_BUCKET_META.every(
    (b) => buckets[b.key].length === 0,
  );

  if (isAllCaughtUp) {
    return (
      <p className="text-sm text-gray-500">
        You&apos;re all caught up! No jobs are overdue or due in the next 30
        days.
      </p>
    );
  }

  return (
    <div className="space-y-10">
      {DUE_BUCKET_META.map((b) => (
        <DueJobSection
          key={b.key}
          id={b.key}
          title={b.title}
          icon={<DueBucketIcon bucket={b.key} className={`h-3.5 w-3.5 ${b.iconColor}`} />}
          iconBg={b.iconBg}
          textClass={b.textClass}
          items={buckets[b.key]}
        />
      ))}
    </div>
  );
}
