"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import type { StepTemplateDto } from "@/lib/api-client";
import { InlineEditableText } from "@/components/inline-editable-text";
import { updateJobDefinition } from "@/app/job-definitions/actions";

interface Props {
  definitionId: string;
  stepTemplates: StepTemplateDto[];
}

export function StepTemplateList({ definitionId, stepTemplates }: Props) {
  const router = useRouter();
  const [draftDescription, setDraftDescription] = useState("");
  const [addError, setAddError] = useState<string | null>(null);
  const [reorderError, setReorderError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    const desc = draftDescription.trim();
    if (!desc) { setAddError("Description is required"); return; }
    if (desc.length > 500) { setAddError("Description must be 500 characters or fewer"); return; }
    setAddError(null);
    startTransition(async () => {
      const result = await updateJobDefinition(definitionId, {
        addStepTemplates: [{ description: desc }],
      });
      if (!result.ok) { setAddError(result.error); return; }
      setDraftDescription("");
      router.refresh();
    });
  }

  function handleRemove(templateId: string) {
    startTransition(async () => {
      const result = await updateJobDefinition(definitionId, {
        removeStepTemplateIds: [templateId],
      });
      if (!result.ok) return;
      router.refresh();
    });
  }

  function handleMove(index: number, direction: "up" | "down") {
    const next = [...stepTemplates];
    const target = direction === "up" ? index - 1 : index + 1;
    if (target < 0 || target >= next.length) return;
    [next[index], next[target]] = [next[target], next[index]];
    setReorderError(null);
    startTransition(async () => {
      const result = await updateJobDefinition(definitionId, {
        reorderStepTemplateIds: next.map((s) => s.id),
      });
      if (!result.ok) { setReorderError(result.error); return; }
      router.refresh();
    });
  }

  const sorted = [...stepTemplates].sort((a, b) => a.order - b.order);

  return (
    <div className="space-y-3">
      {sorted.length === 0 ? (
        <p className="text-sm text-gray-500">No step templates yet.</p>
      ) : (
        <ul className="space-y-1">
          {sorted.map((template, i) => (
            <li key={template.id} className="flex items-start gap-2 py-1">
              <div className="flex flex-col gap-0.5 pt-0.5">
                <button
                  type="button"
                  onClick={() => handleMove(i, "up")}
                  disabled={pending || i === 0}
                  aria-label={`Move "${template.description}" up`}
                  className="rounded px-1 text-xs text-gray-500 hover:bg-gray-100 disabled:opacity-30"
                >
                  ^
                </button>
                <button
                  type="button"
                  onClick={() => handleMove(i, "down")}
                  disabled={pending || i === sorted.length - 1}
                  aria-label={`Move "${template.description}" down`}
                  className="rounded px-1 text-xs text-gray-500 hover:bg-gray-100 disabled:opacity-30"
                >
                  v
                </button>
              </div>

              <div className="flex-1 text-sm text-gray-900">
                <InlineEditableText
                  value={template.description}
                  maxLength={500}
                  ariaLabel={`Edit description for step ${i + 1}`}
                  save={async (val) => {
                    const result = await updateJobDefinition(definitionId, {
                      editStepTemplates: [{ id: template.id, description: val.trim() }],
                    });
                    if (result.ok) { router.refresh(); return { ok: true }; }
                    return { ok: false, error: result.error };
                  }}
                />
              </div>

              <button
                type="button"
                onClick={() => handleRemove(template.id)}
                disabled={pending}
                aria-label={`Remove step template "${template.description}"`}
                className="rounded px-2 py-1 text-xs text-red-600 hover:bg-red-50 disabled:cursor-not-allowed disabled:text-gray-400"
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
      )}

      {reorderError && <p className="text-xs text-red-600">{reorderError}</p>}

      <form onSubmit={handleAdd} className="flex gap-2 pt-2">
        <input
          type="text"
          value={draftDescription}
          onChange={(e) => setDraftDescription(e.target.value)}
          placeholder="Add a step template"
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

      {addError && <p className="text-xs text-red-600">{addError}</p>}
    </div>
  );
}
