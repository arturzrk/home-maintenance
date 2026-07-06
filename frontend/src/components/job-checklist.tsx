"use client";

import { useEffect, useState, useTransition } from "react";
import { addStep, reorderSteps } from "@/app/jobs/actions";
import type { JobDetail, StepDto } from "@/lib/api-client";
import { StepRow } from "@/components/step-row";

/**
 * Hosts the step list, the add-step form, and the reorder controls.
 * Maintains an optimistic local order for snappy up/down moves.
 */
export function JobChecklist({
  jobId,
  initialSteps,
  jobLocked,
}: {
  jobId: string;
  initialSteps: StepDto[];
  jobLocked: boolean;
}) {
  const [steps, setSteps] = useState(initialSteps);
  const [draftDescription, setDraftDescription] = useState("");

  // Server actions in StepRow (remove, edit description) revalidate the
  // page, which re-renders the parent with fresh initialSteps. Resync the
  // local list so those mutations show without a hard reload.
  useEffect(() => setSteps(initialSteps), [initialSteps]);
  const [addError, setAddError] = useState<string | null>(null);
  const [reorderError, setReorderError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function handleAdd(formData: FormData) {
    const desc = String(formData.get("description") ?? "").trim();
    if (!desc) {
      setAddError("Description is required");
      return;
    }
    setAddError(null);
    startTransition(async () => {
      const result = await addStep(jobId, desc);
      if (!result.ok) {
        setAddError(result.error);
        return;
      }
      setSteps(result.value.steps);
      setDraftDescription("");
    });
  }

  function handleMove(index: number, direction: "up" | "down") {
    const next = [...steps];
    const target = direction === "up" ? index - 1 : index + 1;
    if (target < 0 || target >= next.length) return;
    [next[index], next[target]] = [next[target], next[index]];
    setSteps(next); // optimistic
    setReorderError(null);
    startTransition(async () => {
      const result = await reorderSteps(jobId, next.map((s) => s.id));
      if (!result.ok) {
        setSteps(initialSteps); // rollback to server truth
        setReorderError(result.error);
      } else {
        setSteps(result.value.steps);
      }
    });
  }

  return (
    <div className="space-y-3">
      {steps.length === 0 ? (
        <p className="text-sm text-gray-500">No steps on this job.</p>
      ) : (
        <ul className="space-y-1">
          {steps.map((step, i) => (
            <StepRow
              key={step.id}
              jobId={jobId}
              step={step}
              index={i}
              total={steps.length}
              jobLocked={jobLocked}
              moving={pending}
              onMove={(dir) => handleMove(i, dir)}
            />
          ))}
        </ul>
      )}
      {reorderError && (
        <p className="text-xs text-red-600">{reorderError}</p>
      )}

      {!jobLocked && (
        <form action={handleAdd} className="flex gap-2 pt-2">
          <input
            type="text"
            name="description"
            value={draftDescription}
            onChange={(e) => setDraftDescription(e.target.value)}
            placeholder="Add a step"
            maxLength={500}
            disabled={pending}
            className="flex-1 rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
          />
          <button
            type="submit"
            disabled={pending || draftDescription.trim().length === 0}
            className="rounded-md bg-gray-900 px-3 py-2 text-sm font-medium text-white shadow-sm hover:bg-gray-800 disabled:opacity-50"
          >
            Add
          </button>
        </form>
      )}
      {addError && <p className="text-xs text-red-600">{addError}</p>}
    </div>
  );
}

// Stable narrow export so callers don't have to import JobDetail just to
// render the checklist.
export type JobChecklistInput = Pick<JobDetail, "id" | "steps" | "status">;
