import Link from "next/link";

const highlights = [
  {
    title: "Properties",
    text: "Track every home or building you look after in one place.",
  },
  {
    title: "Recurring schedules",
    text: "Set a cadence once - boiler service every 6 months - and jobs appear on time, automatically.",
  },
  {
    title: "Step checklists",
    text: "Every job carries its own checklist, so nothing gets skipped and history shows what was done.",
  },
  {
    title: "Assets",
    text: "Boiler, gutters, lawn mower - tie work to the thing itself and see its full maintenance record.",
  },
];

/**
 * Public landing page rendered at "/" for anonymous visitors. Static
 * server component: no client JS, no data fetches.
 */
export function LandingPage() {
  return (
    <div className="mx-auto max-w-3xl space-y-10 py-6">
      <section className="space-y-4 text-center">
        <h1 className="text-4xl font-bold tracking-tight text-gray-900">
          Maintained House
        </h1>
        <p className="mx-auto max-w-xl text-lg text-gray-600">
          Track the maintenance of your home - recurring schedules,
          checklists, and the assets they keep in shape.
        </p>
        <Link
          id="landing-signin-cta"
          href="/signin"
          className="inline-block rounded-md bg-gray-900 px-6 py-3 text-sm font-medium text-white shadow-sm hover:bg-gray-800"
        >
          Sign in to get started
        </Link>
      </section>

      <section className="grid gap-4 sm:grid-cols-2">
        {highlights.map((h) => (
          <div
            key={h.title}
            className="rounded-md border border-gray-200 bg-white px-4 py-4 shadow-sm"
          >
            <h2 className="text-base font-medium text-gray-900">{h.title}</h2>
            <p className="mt-1 text-sm text-gray-500">{h.text}</p>
          </div>
        ))}
      </section>

      <footer className="flex justify-center gap-6 border-t border-gray-200 pt-6 text-xs text-gray-500">
        <a
          href="/user-manual/index.html"
          target="_blank"
          rel="noopener noreferrer"
          aria-label="User guide (opens in a new tab)"
          className="hover:text-gray-900 hover:underline"
        >
          User guide
        </a>
        <Link href="/privacy" className="hover:text-gray-900 hover:underline">
          Privacy policy
        </Link>
        <Link href="/terms" className="hover:text-gray-900 hover:underline">
          Terms of service
        </Link>
      </footer>
    </div>
  );
}
