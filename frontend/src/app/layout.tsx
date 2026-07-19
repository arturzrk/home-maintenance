import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";
import { auth } from "@/lib/auth";
import { checkHealth, getApiInfo } from "@/lib/api-client";
import { signOutAction } from "@/app/actions";
import { SystemMenu } from "@/components/system-menu";

export const metadata: Metadata = {
  title: "Maintained House",
  description:
    "Track the maintenance of your home - recurring schedules, checklists, and the assets they keep in shape.",
};

export default async function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  // auth() (not requireSession): the header must also render on /signin,
  // where there is no session. API info/health are failure-tolerant so an
  // unreachable backend never breaks the shell.
  const [session, healthy, apiInfo] = await Promise.all([
    auth(),
    checkHealth(),
    getApiInfo().catch(() => null),
  ]);

  const identity =
    session?.user?.name ?? session?.user?.email ?? "Account";

  return (
    <html lang="en">
      <body className="min-h-screen bg-gray-50 text-gray-900 antialiased">
        <header className="border-b border-gray-200 bg-white">
          <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
            <div className="flex h-16 items-center justify-between">
              <Link
                href="/"
                className="text-lg font-semibold tracking-tight hover:text-gray-700"
              >
                Maintained House
              </Link>
              {session ? (
                <SystemMenu
                  identity={identity}
                  version={apiInfo?.version ?? null}
                  healthy={healthy}
                  signOutAction={signOutAction}
                />
              ) : (
                <a
                  href="/user-manual/index.html"
                  target="_blank"
                  rel="noopener noreferrer"
                  aria-label="User guide (opens in a new tab)"
                  className="text-sm text-gray-600 hover:text-gray-900 hover:underline"
                >
                  User guide
                </a>
              )}
            </div>
          </div>
        </header>
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
          {children}
        </main>
      </body>
    </html>
  );
}
