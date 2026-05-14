"use client";

import { useState, useTransition } from "react";
import { createProperty, type ActionResult } from "@/app/properties/actions";

export function CreatePropertyForm() {
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function onSubmit(formData: FormData) {
    setError(null);
    startTransition(async () => {
      const result: ActionResult = await createProperty(formData);
      if (!result.ok) setError(result.error);
      else {
        const input = document.querySelector<HTMLInputElement>(
          "input[name='name']",
        );
        if (input) input.value = "";
      }
    });
  }

  return (
    <form action={onSubmit} className="space-y-2">
      <div className="flex gap-2">
        <input
          type="text"
          name="name"
          maxLength={100}
          required
          placeholder="Property name"
          className="flex-1 rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-gray-500 focus:outline-none focus:ring-1 focus:ring-gray-500"
          disabled={pending}
        />
        <button
          type="submit"
          disabled={pending}
          className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-gray-800 disabled:opacity-50"
        >
          {pending ? "Creating..." : "Create"}
        </button>
      </div>
      {error && <p className="text-sm text-red-600">{error}</p>}
    </form>
  );
}
