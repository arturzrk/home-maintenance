"use client";

import { useState, useTransition } from "react";
import { tickStep, untickStep } from "@/app/jobs/actions";
import type { StepDto } from "@/lib/api-client";

export function StepCheckbox({
  jobId,
  step,
  jobLocked,
}: {
  jobId: string;
  step: StepDto;
  jobLocked: boolean;
}) {
  const [optimistic, setOptimistic] = useState(step.isCompleted);
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function toggle() {
    if (jobLocked || pending) return;
    setError(null);
    const next = !optimistic;
    setOptimistic(next);
    startTransition(async () => {
      const result = next
        ? await tickStep(jobId, step.id)
        : await untickStep(jobId, step.id);
      if (!result.ok) {
        setOptimistic(!next); // rollback
        setError(result.error);
      }
    });
  }

  return (
    <li className="flex items-start gap-3 py-1">
      <input
        type="checkbox"
        checked={optimistic}
        onChange={toggle}
        disabled={jobLocked || pending}
        aria-label={`Toggle "${step.description}"`}
        className="mt-1 h-4 w-4 cursor-pointer rounded border-gray-300 disabled:cursor-not-allowed"
      />
      <div className="flex-1">
        <span
          className={
            "text-sm " +
            (optimistic ? "text-gray-500 line-through" : "text-gray-900")
          }
        >
          {step.description}
        </span>
        {error && <p className="text-xs text-red-600">{error}</p>}
      </div>
    </li>
  );
}
