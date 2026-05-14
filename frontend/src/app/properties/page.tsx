import { properties as propertiesApi } from "@/lib/api-client";
import { requireSession } from "@/lib/session";
import { CreatePropertyForm } from "@/components/create-property-form";
import { PropertyCard } from "@/components/property-card";

export const dynamic = "force-dynamic";

export default async function PropertiesPage() {
  const session = await requireSession();
  const { properties } = await propertiesApi.list(session.idToken);

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <header className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">My properties</h1>
        <p className="text-sm text-gray-500">
          Signed in as <span className="font-mono">{session.user?.name ?? "anonymous"}</span>
        </p>
      </header>

      <CreatePropertyForm />

      {properties.length === 0 ? (
        <p className="text-sm text-gray-500">
          No properties yet. Create one above to get started.
        </p>
      ) : (
        <ul className="space-y-2">
          {properties.map((p) => (
            <li key={p.id}>
              <PropertyCard property={p} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
