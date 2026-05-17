"use client";

import { useEffect, useRef, useState, useTransition } from "react";

type Save = (value: string) => Promise<{ ok: true } | { ok: false; error: string }>;

interface Props {
  value: string;
  save: Save;
  /** Read-only when true; renders a static span without edit affordance. */
  disabled?: boolean;
  /** Max characters; matches the server-side limit so the UI rejects early. */
  maxLength?: number;
  /** Rendered when the value is empty (e.g. "No due date"). */
  emptyLabel?: string;
  /** "text" by default; pass "date" for date inputs. */
  inputType?: "text" | "date";
  /** Extra className for the displayed (non-editing) span. */
  className?: string;
  /** Optional aria-label override. */
  ariaLabel?: string;
}

/**
 * Click-to-edit text. While editing, Enter or blur saves; Escape cancels.
 * On Server Action failure, the UI rolls back to the previous value and
 * surfaces the error inline.
 */
export function InlineEditableText({
  value,
  save,
  disabled = false,
  maxLength,
  emptyLabel,
  inputType = "text",
  className,
  ariaLabel,
}: Props) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => setDraft(value), [value]);

  useEffect(() => {
    if (editing) inputRef.current?.focus();
  }, [editing]);

  function commit() {
    if (draft === value) {
      setEditing(false);
      setError(null);
      return;
    }
    startTransition(async () => {
      const result = await save(draft);
      if (!result.ok) {
        setError(result.error);
        setDraft(value); // rollback the displayed draft
        return;
      }
      setError(null);
      setEditing(false);
    });
  }

  function cancel() {
    setDraft(value);
    setError(null);
    setEditing(false);
  }

  if (disabled) {
    return (
      <span className={className} aria-label={ariaLabel}>
        {value || emptyLabel || ""}
      </span>
    );
  }

  if (!editing) {
    return (
      <button
        type="button"
        onClick={() => setEditing(true)}
        className={
          "rounded text-left hover:bg-gray-100 focus:bg-gray-100 focus:outline-none " +
          (className ?? "")
        }
        aria-label={ariaLabel ?? "Edit"}
      >
        {value || (
          <span className="italic text-gray-400">{emptyLabel ?? "Empty"}</span>
        )}
      </button>
    );
  }

  return (
    <span className="inline-flex flex-col gap-1">
      <input
        ref={inputRef}
        type={inputType}
        value={draft}
        maxLength={maxLength}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            e.preventDefault();
            commit();
          } else if (e.key === "Escape") {
            cancel();
          }
        }}
        disabled={pending}
        aria-label={ariaLabel ?? "Edit"}
        className="rounded border border-gray-300 px-2 py-1 text-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
      />
      {error && <span className="text-xs text-red-600">{error}</span>}
    </span>
  );
}
