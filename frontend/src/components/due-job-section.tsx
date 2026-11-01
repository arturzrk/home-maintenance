import { DueJobCard } from "@/components/due-job-card";
import type { DueJobItem } from "@/lib/due-jobs";

export function DueJobSection({
  title,
  items,
}: {
  title: string;
  items: DueJobItem[];
}) {
  if (items.length === 0) return null;

  return (
    <section className="space-y-2">
      <h2 className="text-sm font-semibold text-gray-700">{title}</h2>
      <ul className="space-y-2">
        {items.map((item) => (
          <li key={item.job.id}>
            <DueJobCard item={item} />
          </li>
        ))}
      </ul>
    </section>
  );
}
