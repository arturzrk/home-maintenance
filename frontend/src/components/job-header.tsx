"use client";

import type { JobDetail } from "@/lib/api-client";
import { updateJob } from "@/app/jobs/actions";
import { InlineEditableText } from "@/components/inline-editable-text";

export function JobHeader({ job }: { job: JobDetail }) {
  const locked = job.status === "Completed";

  return (
    <header className="space-y-1">
      <h1 className="text-2xl font-bold tracking-tight">
        <InlineEditableText
          value={job.name}
          disabled={locked}
          maxLength={200}
          ariaLabel="Edit job name"
          className="text-2xl font-bold tracking-tight"
          save={async (val) => {
            const result = await updateJob(job.id, { name: val });
            return result.ok ? { ok: true } : { ok: false, error: result.error };
          }}
        />
      </h1>
      <p className="flex items-center gap-2 text-sm text-gray-500">
        <span>Due:</span>
        <InlineEditableText
          value={job.dueDate ?? ""}
          disabled={locked}
          inputType="date"
          emptyLabel="No due date"
          ariaLabel="Edit due date"
          save={async (val) => {
            const next = val.trim() === "" ? null : val;
            const result = await updateJob(job.id, { dueDate: next });
            return result.ok ? { ok: true } : { ok: false, error: result.error };
          }}
        />
        <span>- Status: {job.status}</span>
      </p>
    </header>
  );
}
