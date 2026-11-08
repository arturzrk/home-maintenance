import Link from "next/link";
import type { DueJobItem } from "@/lib/due-jobs";

export function DueJobCard({
  item,
  textClass,
}: {
  item: DueJobItem;
  textClass: string;
}) {
  return (
    <Link
      href={`/jobs/${item.job.id}`}
      className="flex items-center justify-between gap-4 rounded-md px-1 py-1 transition hover:bg-gray-100/60"
    >
      <span className="min-w-0">
        <span className="block truncate text-sm font-medium text-gray-900">
          {item.job.name}
        </span>
        <span className="block truncate text-xs text-gray-500">{item.propertyName}</span>
      </span>
      <span className={`shrink-0 text-sm ${textClass}`}>{item.dueLabel}</span>
    </Link>
  );
}
