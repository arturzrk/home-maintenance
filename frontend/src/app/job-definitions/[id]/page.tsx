import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError, assets as assetsApi, jobDefinitions as jobDefinitionsApi, jobs as jobsApi } from "@/lib/api-client";
import { requireSession } from "@/lib/session";
import { DefinitionHeader } from "./components/DefinitionHeader";
import { StepTemplateList } from "./components/StepTemplateList";
import { GenerateNextButton } from "./components/GenerateNextButton";

export const dynamic = "force-dynamic";

export default async function JobDefinitionDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const session = await requireSession();

  let definition;
  try {
    definition = await jobDefinitionsApi.get(id, session.idToken);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      notFound();
    }
    throw err;
  }

  // Filter jobs by definitionId. The backend may not support the definitionId
  // query param yet; if the filter has no effect, client-side filtering below
  // ensures correctness.
  const { jobs: allJobs } = await jobsApi.list(session.idToken, { definitionId: id });
  const generatedJobs = allJobs.filter((j) => j.jobDefinitionId === id);

  // Swallow only a 404 (asset gone); NEXT_REDIRECT and other API
  // failures must propagate.
  let asset = null;
  if (definition.assetId) {
    try {
      asset = await assetsApi.get(definition.assetId, session.idToken);
    } catch (err) {
      if (!(err instanceof ApiError && err.status === 404)) throw err;
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <p className="text-xs text-gray-500">
        <Link href={`/properties/${definition.propertyId}`} className="hover:underline">
          Back to property
        </Link>
      </p>

      <DefinitionHeader definition={definition} />

      {asset && (
        <p className="text-sm text-gray-500">
          Asset:{" "}
          <Link href={`/assets/${asset.id}`} className="font-medium text-gray-900 hover:underline">
            {asset.name}
          </Link>
        </p>
      )}

      <section className="rounded-md border border-gray-200 bg-white p-4 shadow-sm space-y-2">
        <h2 className="text-sm font-semibold text-gray-700">Generated jobs</h2>
        {generatedJobs.length === 0 ? (
          <p className="text-sm text-gray-500">No jobs generated yet.</p>
        ) : (
          <ul className="space-y-1">
            {generatedJobs.map((job) => (
              <li key={job.id} className="flex items-center justify-between text-sm">
                <Link
                  href={`/jobs/${job.id}`}
                  className="font-medium text-gray-900 hover:underline"
                >
                  {job.name}
                </Link>
                <span className="text-gray-500">
                  {job.dueDate
                    ? new Date(job.dueDate).toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" })
                    : "No due date"}
                  {" "}· {job.status}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="rounded-md border border-gray-200 bg-white p-4 shadow-sm space-y-2">
        <h2 className="text-sm font-semibold text-gray-700">Step templates</h2>
        <StepTemplateList
          definitionId={definition.id}
          stepTemplates={definition.stepTemplates}
        />
      </section>

      <section>
        <GenerateNextButton definitionId={definition.id} />
      </section>
    </div>
  );
}
