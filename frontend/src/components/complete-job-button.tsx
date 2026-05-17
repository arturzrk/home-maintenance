"use client";

import { useState, useTransition } from "react";
import { completeJob } from "@/app/jobs/actions";
import type { JobDetail } from "@/lib/api-client";

export function CompleteJobButton({ job }: { job: JobDetail }) {
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  const completed = job.status === "Completed";
  const allDone =
    job.steps.length > 0 && job.steps.every((s) => s.isCompleted);

  if (completed) {
    const when = job.completedAt
      ? new Date(job.completedAt).toLocaleString()
      : "(unknown time)";
    return (
      <p className="text-sm text-green-700">
        Completed on {when}.
      </p>
    );
  }

  function onClick() {
    setError(null);
    startTransition(async () => {
      const result = await completeJob(job.id);
      if (!result.ok) setError(result.error);
    });
  }

  return (
    <div className="space-y-2">
      <button
        type="button"
        onClick={onClick}
        disabled={!allDone || pending}
        className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {pending ? "Completing..." : "Complete job"}
      </button>
      {!allDone && (
        <p className="text-xs text-gray-500">
          All steps must be ticked before the job can be completed.
        </p>
      )}
      {error && <p className="text-sm text-red-600">{error}</p>}
    </div>
  );
}
