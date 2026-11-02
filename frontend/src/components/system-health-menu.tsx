"use client";

import { useEffect, useRef, useState } from "react";
import type { ApiInfo } from "@/types/api";

interface Props {
  healthy: boolean;
  apiInfo: ApiInfo | null;
}

/**
 * Header system-health indicator: a status-dot trigger opening a
 * dropdown with backend connectivity details. Same open/close
 * interaction pattern as SystemMenu, but focused purely on API
 * health - account actions live in SystemMenu, not here.
 */
export function SystemHealthMenu({ healthy, apiInfo }: Props) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function onMouseDown(e: MouseEvent) {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("mousedown", onMouseDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onMouseDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        id="system-health-menu-trigger"
        aria-expanded={open}
        aria-controls="system-health-menu-panel"
        onClick={() => setOpen((o) => !o)}
        className="flex items-center gap-1.5 rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
      >
        <span
          aria-hidden="true"
          className={`inline-block h-2 w-2 rounded-full ${healthy ? "bg-green-500" : "bg-red-500"}`}
        />
        System Health
        <span aria-hidden="true" className="text-xs text-gray-400">▾</span>
      </button>

      {open && (
        <div
          id="system-health-menu-panel"
          className="absolute right-0 z-10 mt-2 w-72 rounded-md border border-gray-200 bg-white p-4 shadow-lg"
        >
          <p className={`text-base font-semibold ${healthy ? "text-green-700" : "text-red-700"}`}>
            {healthy ? "Connected" : "Unreachable"}
          </p>

          {apiInfo && (
            <dl className="mt-2 space-y-1 text-sm text-gray-500">
              <div className="flex gap-2">
                <dt className="font-medium text-gray-600">Service:</dt>
                <dd>{apiInfo.service}</dd>
              </div>
              <div className="flex gap-2">
                <dt className="font-medium text-gray-600">Version:</dt>
                <dd>{apiInfo.version}</dd>
              </div>
            </dl>
          )}

          {!healthy && (
            <p className="mt-3 text-xs text-gray-400">
              Ensure the backend API and MongoDB are running via{" "}
              <code className="rounded bg-gray-100 px-1">docker compose up</code>.
            </p>
          )}
        </div>
      )}
    </div>
  );
}
