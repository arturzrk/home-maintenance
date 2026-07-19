import Link from "next/link";

interface Props {
  title: string;
  updated: string;
  children: React.ReactNode;
}

/**
 * Shared shell for public legal pages (/privacy, /terms): consistent
 * typography without adding a markdown or prose dependency.
 */
export function LegalPage({ title, updated, children }: Props) {
  return (
    <div className="mx-auto max-w-3xl space-y-6 py-4">
      <p className="text-xs text-gray-500">
        <Link href="/" className="hover:underline">
          Back to Maintained House
        </Link>
      </p>
      <header>
        <h1 className="text-2xl font-bold tracking-tight text-gray-900">
          {title}
        </h1>
        <p className="mt-1 text-sm text-gray-500">Last updated: {updated}</p>
      </header>
      <div className="space-y-5 text-sm leading-6 text-gray-700 [&_h2]:mt-6 [&_h2]:text-base [&_h2]:font-semibold [&_h2]:text-gray-900 [&_ul]:list-disc [&_ul]:pl-5 [&_ul]:space-y-1">
        {children}
      </div>
    </div>
  );
}
