"use client";

import type { Property } from "@/lib/api-client";
import { renameProperty } from "@/app/properties/actions";
import { InlineEditableText } from "@/components/inline-editable-text";

export function PropertyHeader({ property }: { property: Property }) {
  return (
    <header className="space-y-1">
      <p className="text-xs text-gray-500">Property</p>
      <h1 className="text-2xl font-bold tracking-tight">
        <InlineEditableText
          value={property.name}
          maxLength={100}
          ariaLabel="Edit property name"
          className="text-2xl font-bold tracking-tight"
          save={async (val) => {
            const result = await renameProperty(property.id, val);
            return result.ok ? { ok: true } : { ok: false, error: result.error };
          }}
        />
      </h1>
    </header>
  );
}
