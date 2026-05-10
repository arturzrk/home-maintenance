interface ConnectionStatusProps {
  healthy: boolean;
  apiInfo: { service: string; version: string; status: string } | null;
}

/**
 * Displays whether the frontend can reach the backend API.
 * This is the only component in the minimal working set — it will be
 * replaced by real dashboard content as features are added.
 */
export function ConnectionStatus({ healthy, apiInfo }: ConnectionStatusProps) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <div className="flex items-center gap-3">
        <span
          className={`inline-flex h-3 w-3 rounded-full ${
            healthy ? "bg-green-500" : "bg-red-500"
          }`}
          aria-hidden="true"
        />
        <h2 className="text-sm font-medium text-gray-700">
          Backend connection
        </h2>
      </div>

      <p
        className={`mt-2 text-2xl font-semibold ${
          healthy ? "text-green-700" : "text-red-700"
        }`}
      >
        {healthy ? "Connected" : "Unreachable"}
      </p>

      {apiInfo && (
        <dl className="mt-4 space-y-1 text-sm text-gray-500">
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
  );
}
