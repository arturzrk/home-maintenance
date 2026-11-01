import Link from "next/link";
import { DUE_BUCKET_META } from "@/components/due-bucket-meta";
import type { DueBucket, DueJobItem } from "@/lib/due-jobs";

const PILL_CLASS_BY_BUCKET: Record<DueBucket, string> = Object.fromEntries(
  DUE_BUCKET_META.map((b) => [b.key, b.pillClass]),
) as Record<DueBucket, string>;

export function DueJobCard({ item }: { item: DueJobItem }) {
  return (
    <Link
      href={`/jobs/${item.job.id}`}
      className="block rounded-md border border-gray-200 bg-white px-4 py-3 shadow-sm transition hover:border-gray-300 hover:bg-gray-50"
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-base font-medium text-gray-900">
          {item.job.name}
        </span>
        <span
          className={`rounded-full px-2 py-0.5 text-xs font-medium ${PILL_CLASS_BY_BUCKET[item.bucket]}`}
        >
          {item.dueLabel}
        </span>
      </div>
      <p className="mt-1 text-xs text-gray-500">{item.propertyName}</p>
    </Link>
  );
}
