"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import type { AssetDto } from "@/lib/api-client";
import { updateAsset } from "@/app/assets/actions";
import { InlineEditableText } from "@/components/inline-editable-text";

export function AssetHeader({ asset }: { asset: AssetDto }) {
  const router = useRouter();
  const [toggleError, setToggleError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function toggleObsolete() {
    setToggleError(null);
    startTransition(async () => {
      const result = await updateAsset(asset.id, { isObsolete: !asset.isObsolete });
      if (!result.ok) {
        setToggleError(result.error);
        return;
      }
      router.refresh();
    });
  }

  return (
    <header className="space-y-2">
      <div className="flex items-center gap-2">
        <h1 className="text-2xl font-bold tracking-tight">
          <InlineEditableText
            value={asset.name}
            maxLength={200}
            ariaLabel="Edit asset name"
            className="text-2xl font-bold tracking-tight"
            save={async (val) => {
              const result = await updateAsset(asset.id, { name: val });
              if (result.ok) { router.refresh(); return { ok: true }; }
              return { ok: false, error: result.error };
            }}
          />
        </h1>
        {asset.isObsolete && (
          <span className="rounded-full bg-gray-200 px-2 py-0.5 text-xs font-medium text-gray-700">
            Obsolete
          </span>
        )}
      </div>

      <p className="flex items-center gap-2 text-sm text-gray-500">
        <span>Category:</span>
        <InlineEditableText
          value={asset.category ?? ""}
          maxLength={100}
          emptyLabel="No category"
          ariaLabel="Edit asset category"
          save={async (val) => {
            // Empty string clears the category (backend PATCH semantics).
            const result = await updateAsset(asset.id, { category: val.trim() });
            if (result.ok) { router.refresh(); return { ok: true }; }
            return { ok: false, error: result.error };
          }}
        />
      </p>

      <p className="flex items-center gap-2 text-sm text-gray-500">
        <span>Notes:</span>
        <InlineEditableText
          value={asset.notes ?? ""}
          maxLength={2000}
          emptyLabel="No notes"
          ariaLabel="Edit asset notes"
          save={async (val) => {
            const result = await updateAsset(asset.id, { notes: val.trim() });
            if (result.ok) { router.refresh(); return { ok: true }; }
            return { ok: false, error: result.error };
          }}
        />
      </p>

      <div>
        <button
          type="button"
          onClick={toggleObsolete}
          disabled={pending}
          className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-600 hover:bg-gray-50 disabled:opacity-50"
        >
          {pending
            ? "Saving..."
            : asset.isObsolete
              ? "Reactivate"
              : "Mark obsolete"}
        </button>
        {toggleError && <p className="mt-1 text-sm text-red-600">{toggleError}</p>}
      </div>
    </header>
  );
}
