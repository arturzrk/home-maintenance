"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { createJobDefinition } from "@/app/properties/actions";
import type { AssetDto, ScheduleDefinitionDto } from "@/lib/api-client";

interface Props {
  propertyId: string;
  assets?: AssetDto[];
}

export function CreateJobDefinitionForm({ propertyId, assets = [] }: Props) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  const [name, setName] = useState("");
  const [unit, setUnit] = useState<ScheduleDefinitionDto["unit"]>("Month");
  const [multiplier, setMultiplier] = useState(1);
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [assetId, setAssetId] = useState("");
  const [steps, setSteps] = useState<string[]>([]);

  function addStep() {
    setSteps((prev) => [...prev, ""]);
  }

  function removeStep(i: number) {
    setSteps((prev) => prev.filter((_, idx) => idx !== i));
  }

  function updateStep(i: number, val: string) {
    setSteps((prev) => prev.map((s, idx) => (idx === i ? val : s)));
  }

  function reset() {
    setName("");
    setUnit("Month");
    setMultiplier(1);
    setStartDate("");
    setEndDate("");
    setAssetId("");
    setSteps([]);
    setError(null);
  }

  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) { setError("Name is required"); return; }
    if (multiplier < 1) { setError("Multiplier must be at least 1"); return; }
    if (!startDate) { setError("Start date is required"); return; }

    setError(null);
    startTransition(async () => {
      const result = await createJobDefinition(propertyId, {
        name: name.trim(),
        schedule: { unit, multiplier, startDate, endDate: endDate || null },
        stepTemplates: steps
          .map((d) => ({ description: d.trim() }))
          .filter((s) => s.description.length > 0),
        assetId: assetId || null,
      });
      if (!result.ok) {
        setError(result.error);
        return;
      }
      reset();
      setOpen(false);
      router.refresh();
    });
  }

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="rounded-md border border-dashed border-gray-300 px-4 py-2 text-sm text-gray-600 hover:border-gray-400 hover:text-gray-900"
      >
        + Add recurring job
      </button>
    );
  }

  return (
    <form
      onSubmit={onSubmit}
      noValidate
      className="space-y-3 rounded-md border border-gray-200 bg-white p-4 shadow-sm"
    >
      <h2 className="text-sm font-semibold text-gray-700">Add recurring job</h2>

      <div>
        <label className="block text-xs text-gray-600" htmlFor="jd-name">
          Name
        </label>
        <input
          id="jd-name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
          maxLength={200}
          placeholder="e.g. Service boiler"
          disabled={pending}
          className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
        />
      </div>

      <div className="flex gap-3">
        <div className="flex-1">
          <label className="block text-xs text-gray-600" htmlFor="jd-multiplier">
            Every
          </label>
          <input
            id="jd-multiplier"
            type="number"
            min={1}
            value={multiplier}
            onChange={(e) => setMultiplier(Number(e.target.value))}
            disabled={pending}
            className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
          />
        </div>
        <div className="flex-1">
          <label className="block text-xs text-gray-600" htmlFor="jd-unit">
            Unit
          </label>
          <select
            id="jd-unit"
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
        <label className="block text-xs text-gray-600" htmlFor="jd-start">
          Start date
        </label>
        <input
          id="jd-start"
          type="date"
          value={startDate}
          onChange={(e) => setStartDate(e.target.value)}
          required
          disabled={pending}
          className="mt-1 rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
        />
      </div>

      <div>
        <label className="block text-xs text-gray-600" htmlFor="jd-end">
          End date (optional)
        </label>
        <input
          id="jd-end"
          type="date"
          value={endDate}
          onChange={(e) => setEndDate(e.target.value)}
          disabled={pending}
          className="mt-1 rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
        />
      </div>

      {assets.length > 0 && (
        <div>
          <label className="block text-xs text-gray-600" htmlFor="jd-asset">
            Asset (optional)
          </label>
          <select
            id="jd-asset"
            value={assetId}
            onChange={(e) => setAssetId(e.target.value)}
            disabled={pending}
            className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
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
        <p className="text-xs text-gray-600">Steps (optional)</p>
        <ul className="mt-1 space-y-1">
          {steps.map((s, i) => (
            <li key={i} className="flex gap-2">
              <input
                type="text"
                value={s}
                onChange={(e) => updateStep(i, e.target.value)}
                maxLength={500}
                placeholder={`Step ${i + 1}`}
                disabled={pending}
                aria-label={`Step ${i + 1}`}
                className="flex-1 rounded-md border border-gray-300 px-3 py-1.5 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
              />
              <button
                type="button"
                onClick={() => removeStep(i)}
                disabled={pending}
                className="rounded px-2 py-1 text-xs text-red-600 hover:bg-red-50 disabled:text-gray-400"
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
        <button
          type="button"
          onClick={addStep}
          disabled={pending}
          className="mt-1 text-xs text-gray-600 hover:text-gray-900 disabled:text-gray-400"
        >
          + Add step
        </button>
      </div>

      {error && <p className="text-sm text-red-600">{error}</p>}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={pending}
          className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-gray-800 disabled:opacity-50"
        >
          {pending ? "Saving..." : "Save recurring job"}
        </button>
        <button
          type="button"
          onClick={() => { reset(); setOpen(false); }}
          disabled={pending}
          className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-600 hover:bg-gray-50 disabled:opacity-50"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
