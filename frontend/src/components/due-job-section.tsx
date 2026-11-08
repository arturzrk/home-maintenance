import type { ReactNode } from "react";
import { DueJobCard } from "@/components/due-job-card";
import type { DueJobItem } from "@/lib/due-jobs";

export function DueJobSection({
  id,
  title,
  icon,
  iconBg,
  textClass,
  items,
}: {
  id: string;
  title: string;
  icon: ReactNode;
  iconBg: string;
  textClass: string;
  items: DueJobItem[];
}) {
  if (items.length === 0) return null;

  const headingId = `due-job-section-${id}`;

  return (
    <section aria-labelledby={headingId}>
      <div className="flex items-baseline justify-between gap-4 border-b border-gray-200 pb-3">
        <h2
          id={headingId}
          className="flex items-center gap-3 text-lg font-semibold text-gray-900"
        >
          <span
            className={`inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full ${iconBg}`}
          >
            {icon}
          </span>
          {title}
        </h2>
        <span className="flex items-baseline gap-1.5">
          <span className={`text-2xl font-bold leading-none ${textClass}`}>
            {items.length}
          </span>
          <span className="text-xs font-medium uppercase tracking-wide text-gray-400">
            jobs
          </span>
        </span>
      </div>
      <ul className="mt-4 space-y-4">
        {items.map((item) => (
          <li key={item.job.id}>
            <DueJobCard item={item} textClass={textClass} />
          </li>
        ))}
      </ul>
    </section>
  );
}
