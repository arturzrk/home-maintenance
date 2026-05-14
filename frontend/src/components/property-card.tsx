import Link from "next/link";
import type { Property } from "@/lib/api-client";

export function PropertyCard({ property }: { property: Property }) {
  return (
    <Link
      href={`/properties/${property.id}`}
      className="block rounded-md border border-gray-200 bg-white px-4 py-3 shadow-sm transition hover:border-gray-300 hover:bg-gray-50"
    >
      <span className="text-base font-medium text-gray-900">{property.name}</span>
    </Link>
  );
}
