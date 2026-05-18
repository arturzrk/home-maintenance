# Observability (Application Insights via OpenTelemetry)

The backend ships with the [Azure Monitor OpenTelemetry distro]
(https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable).
When the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable
is set, it auto-instruments:

- **Every HTTP request** as a span (path, status code, duration).
- **Every outbound HttpClient call** as a dependency span.
- **Every unhandled exception** with the full stack trace.
- Default ASP.NET Core / runtime metrics (request rate, response
  time histograms, GC, thread pool).

When the env var is unset, OpenTelemetry is **not** registered at all -
the app runs with zero telemetry overhead. Local dev stays untouched.

## One-time setup for staging

1. **Create an Application Insights resource** in the Azure portal:
   - Search: "Application Insights" -> Create.
   - Subscription / Resource group: same as the App Service.
   - Name: `home-maintenance-staging-insights` (or similar).
   - Region: same as the App Service.
   - Resource Mode: **Workspace-based** (Microsoft's current default).
   - Log Analytics workspace: pick or create one (free tier is fine).
   - Click "Review + create" -> "Create".

2. **Copy the Connection String** from the resource overview page
   (looks like
   `InstrumentationKey=...;IngestionEndpoint=https://...applicationinsights.azure.com/`).

3. **Set the env var on the App Service**:
   - Portal -> your App Service -> Configuration -> Application
     settings -> "+ New application setting".
   - Name: `APPLICATIONINSIGHTS_CONNECTION_STRING`
   - Value: paste from step 2.
   - Save (the app restarts).

4. **Wait ~2 minutes**, then visit the App Insights resource ->
   "Live metrics" (under Investigate). Hit any endpoint and you
   should see the request appear in real time.

## What to look at

| Pane | What it shows |
|---|---|
| **Live metrics** | Real-time request rate, response time, failure rate. Useful for "is anything reaching the server right now?" |
| **Application Map** | Auto-generated topology. Should show the API + the dependencies it talks to (Mongo appears once MongoDB instrumentation is added). |
| **Failures** | Exceptions and 4xx/5xx responses, grouped by operation. The 401 problem-details responses our app emits are visible here. |
| **Performance** | Request duration percentiles per endpoint. Useful for verifying SC-004 (step-tick p95 under 500ms) in production-ish conditions. |
| **Logs (Analytics)** | KQL query workspace. Examples: |

```kql
// Slowest 20 step-tick requests in the last hour
requests
| where timestamp > ago(1h)
| where name contains "TickStep"
| top 20 by duration desc
| project timestamp, name, duration, resultCode, operation_Id

// Failure breakdown by HTTP status
requests
| where timestamp > ago(1d)
| where success == false
| summarize count() by resultCode
| render columnchart

// Correlate an audit-log entry with the originating request
// (paste the correlationId from audit-trail/property-job-step.jsonl)
requests
| where customDimensions["CorrelationId"] == "<paste-correlationId>"
```

## Costs and quotas

- Free tier: **5 GB ingested per month**, 90-day retention.
- Beyond 5 GB: ~$2.30 per GB (varies by region).
- A personal-scale app at staging traffic should fit inside the free
  tier comfortably.
- The OpenTelemetry distro samples server-side by default at 100%; for
  higher-volume environments, configure
  [adaptive sampling](https://learn.microsoft.com/azure/azure-monitor/app/sampling)
  on the App Insights resource.

## Adding MongoDB dependency tracing (optional follow-up)

The distro auto-instruments ASP.NET Core + HttpClient but **not**
MongoDB by default. To see Mongo calls as dependency spans in the
Application Map, add the MongoDB.Driver diagnostic source:

```bash
dotnet add backend/src/HomeMaintenance.Infrastructure package MongoDB.Driver.Core.Extensions.DiagnosticSources
```

Then in `Infrastructure/DependencyInjection.cs`, replace the bare
`new MongoClient(connectionString)` with a configured `MongoClientSettings`
that adds the `DiagnosticsActivityEventSubscriber`. Defer until you
actually want this signal - the basic ASP.NET Core trace is enough
to start.

## Turning telemetry off

Two ways:

- **Production-style**: leave the env var unset. The
  `if (!string.IsNullOrEmpty(...))` guard in `Program.cs` skips
  OpenTelemetry registration entirely.
- **Runtime sampling**: configure adaptive sampling on the App
  Insights resource to drop spans before they're sent. Useful for
  cost control without removing the signal.

## How this interacts with the audit log

App Insights and the audit log answer different questions and live
in different places:

| | Audit log (`audit-trail/property-job-step.jsonl`) | Application Insights |
|---|---|---|
| Purpose | Append-only record of business events (who created what, when) | Per-request operational telemetry |
| Source of truth for | Compliance, "who did what to which resource" | Performance, failure rates, debugging |
| Retention | 1 year (per constitution) | 90 days (free tier) |
| Format | JSONL | Azure Monitor schema |
| Correlation | Carries `correlationId` from the X-Correlation-Id header | Same `correlationId` exposed as `customDimensions.CorrelationId` |

You can pivot from an audit log entry to its request in App Insights
by copying the `correlationId` and running the KQL query above.
