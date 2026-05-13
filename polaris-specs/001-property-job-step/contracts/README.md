# REST Contracts: 001-property-job-step

The HTTP-level contract for every endpoint in Slice 1. Used as the
authoritative reference for the typed frontend client
(`frontend/src/lib/api-client.ts`) and the integration tests.

## Conventions

- Base URL: `/api`.
- All endpoints (except `/health` outside `/api`) require
  `Authorization: Bearer <token>`. Anonymous requests return `401`.
- Cross-owner access returns `404` (no `403` - see edge cases in spec).
- All bodies are JSON; all timestamps are ISO-8601 UTC; all dates are
  ISO-8601 date strings without timezone (`YYYY-MM-DD`).
- Error responses are RFC 7807 problem details:
  ```json
  {
    "type": "https://home-maintenance/errors/<code>",
    "title": "<human-readable summary>",
    "status": <http-status>,
    "code": "<machine-readable code from Result error>",
    "detail": "<message>",
    "correlationId": "<uuid>"
  }
  ```
- Every response carries `X-Correlation-Id` (request-scoped UUID) so the
  audit log entry can be correlated.

## Contracts

| Resource | File |
|---|---|
| Properties | [properties.md](./properties.md) |
| Jobs | [jobs.md](./jobs.md) |
| Steps (sub-resource of Jobs) | [steps.md](./steps.md) |

## Error code -> HTTP status mapping

| `code` | HTTP status | Meaning |
|---|---|---|
| `unauthorized` | 401 | Token missing, expired, or invalid |
| `validation` | 400 | Request body fails DTO validation |
| `business_rule` | 400 | Domain rule rejected (e.g., "steps_incomplete") |
| `not_found` | 404 | Resource missing or not owned by caller |
| `forbidden` | 404 | Caller is authenticated but does not own the resource. Returned as 404 deliberately. |
