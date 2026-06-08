"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import type { JobDefinitionDto, ScheduleDefinitionDto } from "@/lib/api-client";
import { InlineEditableText } from "@/components/inline-editable-text";
import { updateJobDefinition } from "@/app/job-definitions/actions";

function scheduleLabel(s: ScheduleDefinitionDto): string {
  const unit = s.multiplier === 1 ? s.unit.toLowerCase() : `${s.multiplier} ${s.unit.toLowerCase()}s`;
  const from = new Date(s.startDate).toLocaleDateString("en-GB", { month: "short", year: "numeric" });
  return `Every ${unit} from ${from}`;
}

interface Props {
  definition: JobDefinitionDto;
}

export function DefinitionHeader({ definition }: Props) {
  const router = useRouter();
  const [scheduleOpen, setScheduleOpen] = useState(false);
  const [scheduleError, setScheduleError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  const [unit, setUnit] = useState<ScheduleDefinitionDto["unit"]>(definition.schedule.unit);
  const [multiplier, setMultiplier] = useState(definition.schedule.multiplier);
  const [startDate, setStartDate] = useState(definition.schedule.startDate);
  const [endDate, setEndDate] = useState(definition.schedule.endDate ?? "");

  function saveSchedule() {
    if (multiplier < 1) { setScheduleError("Multiplier must be at least 1"); return; }
    if (!startDate) { setScheduleError("Start date is required"); return; }
    setScheduleError(null);
    startTransition(async () => {
      const result = await updateJobDefinition(definition.id, {
        schedule: { unit, multiplier, startDate, endDate: endDate || null },
      });
      if (!result.ok) {
        setScheduleError(result.error);
        return;
      }
      setScheduleOpen(false);
      router.refresh();
    });
  }

  function cancelSchedule() {
    setUnit(definition.schedule.unit);
    setMultiplier(definition.schedule.multiplier);
    setStartDate(definition.schedule.startDate);
    setEndDate(definition.schedule.endDate ?? "");
    setScheduleError(null);
    setScheduleOpen(false);
  }

  return (
    <header className="space-y-2">
      <h1 className="text-2xl font-bold tracking-tight">
        <InlineEditableText
          value={definition.name}
          maxLength={200}
          ariaLabel="Edit definition name"
          className="text-2xl font-bold tracking-tight"
          save={async (val) => {
            const result = await updateJobDefinition(definition.id, { name: val.trim() });
            if (result.ok) { router.refresh(); return { ok: true }; }
            return { ok: false, error: result.error };
          }}
        />
      </h1>

      <p className="text-sm text-gray-500">
        {scheduleLabel(definition.schedule)}
        {" "}
        <button
          type="button"
          onClick={() => setScheduleOpen((o) => !o)}
          className="text-xs text-gray-400 underline hover:text-gray-700"
        >
          Edit schedule
        </button>
      </p>

      {scheduleOpen && (
        <div className="rounded-md border border-gray-200 bg-white p-4 shadow-sm space-y-3">
          <h2 className="text-sm font-semibold text-gray-700">Edit schedule</h2>

          <div className="flex gap-3">
            <div className="flex-1">
              <label className="block text-xs text-gray-600" htmlFor="edit-multiplier">Every</label>
              <input
                id="edit-multiplier"
                type="number"
                min={1}
                value={multiplier}
                onChange={(e) => setMultiplier(Number(e.target.value))}
                disabled={pending}
                className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
              />
            </div>
            <div className="flex-1">
              <label className="block text-xs text-gray-600" htmlFor="edit-unit">Unit</label>
              <select
                id="edit-unit"
                value={unit}
                onChange={(e) => setUnit(e.target.value as ScheduleDefinitionDto["unit"])}
                disabled={pending}
                className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
              >
                <option value="Day">Day</option>
                <option value="Week">Week</option>
                <option value="Month">Month</option>
                <option value="Year">Year</option>
              </select>
            </div>
          </div>

          <div>
            <label className="block text-xs text-gray-600" htmlFor="edit-start">Start date</label>
            <input
              id="edit-start"
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              disabled={pending}
              className="mt-1 rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
            />
          </div>

          <div>
            <label className="block text-xs text-gray-600" htmlFor="edit-end">End date (optional)</label>
            <input
              id="edit-end"
              type="date"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              disabled={pending}
              className="mt-1 rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
            />
          </div>

          {scheduleError && <p className="text-sm text-red-600">{scheduleError}</p>}

          <div className="flex gap-2">
            <button
              type="button"
              onClick={saveSchedule}
              disabled={pending}
              className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-gray-800 disabled:opacity-50"
            >
              {pending ? "Saving..." : "Save"}
            </button>
            <button
              type="button"
              onClick={cancelSchedule}
              disabled={pending}
              className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-600 hover:bg-gray-50 disabled:opacity-50"
            >
              Cancel
            </button>
          </div>
        </div>
      )}
    </header>
  );
}
