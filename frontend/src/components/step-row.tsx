"use client";

import { useState, useTransition } from "react";
import {
  editStepDescription,
  removeStep,
  tickStep,
  untickStep,
} from "@/app/jobs/actions";
import type { StepDto } from "@/lib/api-client";
import { InlineEditableText } from "@/components/inline-editable-text";

interface Props {
  jobId: string;
  step: StepDto;
  index: number;
  total: number;
  jobLocked: boolean;
  onMove: (direction: "up" | "down") => void;
  moving: boolean;
}

/**
 * One row in the Job checklist. Combines: tick/untick checkbox,
 * inline-edit description, remove button, and up/down reorder buttons.
 * All affordances disable when the parent Job is Completed.
 */
export function StepRow({
  jobId,
  step,
  index,
  total,
  jobLocked,
  onMove,
  moving,
}: Props) {
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
        setOptimistic(!next);
        setError(result.error);
      }
    });
  }

  function handleRemove() {
    if (jobLocked || pending) return;
    setError(null);
    startTransition(async () => {
      const result = await removeStep(jobId, step.id);
      if (!result.ok) setError(result.error);
    });
  }

  const busy = pending || moving;

  return (
    <li className="flex items-start gap-2 py-1">
      <div className="flex flex-col gap-0.5 pt-0.5">
        <button
          type="button"
          onClick={() => onMove("up")}
          disabled={jobLocked || busy || index === 0}
          aria-label={`Move "${step.description}" up`}
          className="rounded px-1 text-xs text-gray-500 hover:bg-gray-100 disabled:opacity-30"
        >
          ^
        </button>
        <button
          type="button"
          onClick={() => onMove("down")}
          disabled={jobLocked || busy || index === total - 1}
          aria-label={`Move "${step.description}" down`}
          className="rounded px-1 text-xs text-gray-500 hover:bg-gray-100 disabled:opacity-30"
        >
          v
        </button>
      </div>

      <input
        type="checkbox"
        checked={optimistic}
        onChange={toggle}
        disabled={jobLocked || busy}
        aria-label={`Toggle "${step.description}"`}
        className="mt-1 h-4 w-4 cursor-pointer rounded border-gray-300 disabled:cursor-not-allowed"
      />

      <div className="flex-1">
        <span
          className={
            optimistic ? "text-sm text-gray-500 line-through" : "text-sm text-gray-900"
          }
        >
          <InlineEditableText
            value={step.description}
            disabled={jobLocked || optimistic}
            maxLength={500}
            ariaLabel={`Edit description for step ${index + 1}`}
            save={async (val) => {
              const result = await editStepDescription(jobId, step.id, val);
              return result.ok ? { ok: true } : { ok: false, error: result.error };
            }}
          />
        </span>
        {error && <p className="text-xs text-red-600">{error}</p>}
      </div>

      <button
        type="button"
        onClick={handleRemove}
        disabled={jobLocked || busy}
        aria-label={`Remove step "${step.description}"`}
        className="rounded px-2 py-1 text-xs text-red-600 hover:bg-red-50 disabled:cursor-not-allowed disabled:text-gray-400"
      >
        Remove
      </button>
    </li>
  );
}
