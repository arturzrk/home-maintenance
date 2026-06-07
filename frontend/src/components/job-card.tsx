import Link from "next/link";
import type { JobSummary } from "@/lib/api-client";

export function JobCard({ job }: { job: JobSummary }) {
  const completed = job.status === "Completed";
  return (
    <Link
      href={`/jobs/${job.id}`}
      className="block rounded-md border border-gray-200 bg-white px-4 py-3 shadow-sm transition hover:border-gray-300 hover:bg-gray-50"
    >
      <div className="flex items-center justify-between">
        <span className="flex items-center gap-2">
          <span className="text-base font-medium text-gray-900">{job.name}</span>
          {job.jobDefinitionId != null && (
            <span className="rounded-full bg-purple-100 px-2 py-0.5 text-xs font-medium text-purple-800">
              Recurring
            </span>
          )}
        </span>
        <span
          className={
            "rounded-full px-2 py-0.5 text-xs font-medium " +
            (completed
              ? "bg-green-100 text-green-800"
              : "bg-blue-100 text-blue-800")
          }
        >
          {job.status}
        </span>
      </div>
      <p className="mt-1 text-xs text-gray-500">
        {job.completedStepCount} of {job.stepCount} steps
        {job.dueDate ? ` - due ${job.dueDate}` : ""}
      </p>
    </Link>
  );
}
