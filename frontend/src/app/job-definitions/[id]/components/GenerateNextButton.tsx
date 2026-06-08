"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { generateNextOccurrence } from "@/app/job-definitions/actions";

interface Props {
  definitionId: string;
}

export function GenerateNextButton({ definitionId }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function handleClick() {
    setError(null);
    startTransition(async () => {
      const result = await generateNextOccurrence(definitionId);
      if (result.ok) {
        router.push(`/jobs/${result.value.id}`);
        return;
      }
      if (result.code === "next_occurrence_already_exists") {
        setError("The next occurrence is already scheduled.");
      } else {
        setError(result.error ?? "Something went wrong. Please try again.");
      }
    });
  }

  return (
    <div className="space-y-2">
      <button
        type="button"
        onClick={handleClick}
        disabled={pending}
        className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-gray-800 disabled:opacity-50"
      >
        {pending ? "Generating..." : "Generate next"}
      </button>
      {error && <p className="text-sm text-red-600">{error}</p>}
    </div>
  );
}
