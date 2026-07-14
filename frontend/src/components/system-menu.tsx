"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";

interface Props {
  identity: string;
  version: string | null;
  healthy: boolean;
  signOutAction: () => Promise<void>;
}

/**
 * Header system menu: identity trigger opening a dropdown with
 * navigation, system info, and sign-out. Closes on outside click,
 * Escape, or choosing a navigation item.
 */
export function SystemMenu({ identity, version, healthy, signOutAction }: Props) {
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
        id="system-menu-trigger"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="System menu"
        onClick={() => setOpen((o) => !o)}
        className="flex items-center gap-1 rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
      >
        {identity}
        <span aria-hidden="true" className="text-xs text-gray-400">▾</span>
      </button>

      {open && (
        <div
          role="menu"
          aria-label="System menu"
          className="absolute right-0 z-10 mt-2 w-64 rounded-md border border-gray-200 bg-white py-1 shadow-lg"
        >
          <Link
            href="/properties"
            role="menuitem"
            onClick={() => setOpen(false)}
            className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
          >
            My properties
          </Link>
          <a
            href="/user-manual/index.html"
            target="_blank"
            rel="noopener noreferrer"
            role="menuitem"
            aria-label="User guide (opens in a new tab)"
            onClick={() => setOpen(false)}
            className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
          >
            User guide
          </a>

          <div className="my-1 border-t border-gray-100" />

          <div className="px-4 py-2 text-xs text-gray-500">
            <p>Version {version ?? "unknown"}</p>
            <p className="mt-0.5 flex items-center gap-1.5">
              <span
                aria-hidden="true"
                className={
                  "inline-block h-2 w-2 rounded-full " +
                  (healthy ? "bg-green-500" : "bg-red-500")
                }
              />
              API: {healthy ? "Connected" : "Unreachable"}
            </p>
          </div>

          <div className="my-1 border-t border-gray-100" />

          <form action={signOutAction}>
            <button
              type="submit"
              role="menuitem"
              className="block w-full px-4 py-2 text-left text-sm text-gray-700 hover:bg-gray-50"
            >
              Sign out
            </button>
          </form>
        </div>
      )}
    </div>
  );
}
