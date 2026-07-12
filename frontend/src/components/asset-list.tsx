"use client";

import { useState, useTransition } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { createAsset, type ActionResult } from "@/app/assets/actions";
import type { AssetDto } from "@/lib/api-client";

interface Props {
  propertyId: string;
  assets: AssetDto[];
}

export function AssetList({ propertyId, assets }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function onSubmit(formData: FormData) {
    setError(null);
    startTransition(async () => {
      const result: ActionResult<{ id: string }> = await createAsset(formData);
      if (!result.ok) {
        setError(result.error);
        return;
      }
      const form = document.getElementById("create-asset-form") as HTMLFormElement | null;
      form?.reset();
      router.refresh();
    });
  }

  return (
    <section className="space-y-2">
      <h2 className="text-sm font-semibold text-gray-700">Assets</h2>

      {assets.length === 0 ? (
        <p className="text-sm text-gray-500">No assets yet.</p>
      ) : (
        <ul className="space-y-2">
          {assets.map((a) => (
            <li key={a.id}>
              <Link
                href={`/assets/${a.id}`}
                className="block rounded-md border border-gray-200 bg-white px-4 py-3 shadow-sm transition hover:border-gray-300 hover:bg-gray-50"
              >
                <div className="flex items-center justify-between">
                  <span className="flex items-center gap-2">
                    <span className="text-base font-medium text-gray-900">{a.name}</span>
                    {a.isObsolete && (
                      <span className="rounded-full bg-gray-200 px-2 py-0.5 text-xs font-medium text-gray-700">
                        Obsolete
                      </span>
                    )}
                  </span>
                  {a.category && (
                    <span className="text-xs text-gray-500">{a.category}</span>
                  )}
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}

      <form
        id="create-asset-form"
        action={onSubmit}
        className="space-y-3 rounded-md border border-gray-200 bg-white p-4 shadow-sm"
      >
        <input type="hidden" name="propertyId" value={propertyId} />

        <h3 className="text-sm font-semibold text-gray-700">Add asset</h3>

        <div>
          <label className="block text-xs text-gray-600" htmlFor="asset-name">
            Name
          </label>
          <input
            id="asset-name"
            name="name"
            required
            maxLength={200}
            placeholder="e.g. Boiler"
            className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
            disabled={pending}
          />
        </div>

        <div>
          <label className="block text-xs text-gray-600" htmlFor="asset-category">
            Category (optional)
          </label>
          <input
            id="asset-category"
            name="category"
            maxLength={100}
            placeholder="e.g. Heating"
            className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
            disabled={pending}
          />
        </div>

        <button
          type="submit"
          disabled={pending}
          className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-gray-800 disabled:opacity-50"
        >
          {pending ? "Adding..." : "Add asset"}
        </button>

        {error && <p className="text-sm text-red-600">{error}</p>}
      </form>
    </section>
  );
}
