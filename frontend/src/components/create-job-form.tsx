"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { createJob, type ActionResult } from "@/app/jobs/actions";
import type { AssetDto } from "@/lib/api-client";

export function CreateJobForm({
  propertyId,
  assets = [],
}: {
  propertyId: string;
  assets?: AssetDto[];
}) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function onSubmit(formData: FormData) {
    setError(null);
    startTransition(async () => {
      const result: ActionResult<{ id: string }> = await createJob(formData);
      if (!result.ok) {
        setError(result.error);
        return;
      }
      // Clear inputs, then navigate to the new Job.
      const form = document.getElementById("create-job-form") as HTMLFormElement | null;
      form?.reset();
      router.push(`/jobs/${result.value.id}`);
    });
  }

  return (
    <form
      id="create-job-form"
      action={onSubmit}
      className="space-y-3 rounded-md border border-gray-200 bg-white p-4 shadow-sm"
    >
      <input type="hidden" name="propertyId" value={propertyId} />

      <h2 className="text-sm font-semibold text-gray-700">Create job</h2>

      <div>
        <label className="block text-xs text-gray-600" htmlFor="job-name">
          Name
        </label>
        <input
          id="job-name"
          name="name"
          required
          maxLength={200}
          placeholder="e.g. Service boiler"
          className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
          disabled={pending}
        />
      </div>

      <div>
        <label className="block text-xs text-gray-600" htmlFor="job-due">
          Due date (optional)
        </label>
        <input
          id="job-due"
          name="dueDate"
          type="date"
          className="mt-1 rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
          disabled={pending}
        />
      </div>

      {assets.length > 0 && (
        <div>
          <label className="block text-xs text-gray-600" htmlFor="job-asset">
            Asset (optional)
          </label>
          <select
            id="job-asset"
            name="assetId"
            defaultValue=""
            className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
            disabled={pending}
          >
            <option value="">No asset</option>
            {assets.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name}
              </option>
            ))}
          </select>
        </div>
      )}

      <div>
        <label className="block text-xs text-gray-600" htmlFor="job-steps">
          Steps (one per line, optional)
        </label>
        <textarea
          id="job-steps"
          name="steps"
          rows={4}
          placeholder="Shut off gas&#10;Drain system&#10;Replace filter"
          className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
          disabled={pending}
        />
      </div>

      <button
        type="submit"
        disabled={pending}
        className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-gray-800 disabled:opacity-50"
      >
        {pending ? "Creating..." : "Create job"}
      </button>

      {error && <p className="text-sm text-red-600">{error}</p>}
    </form>
  );
}
