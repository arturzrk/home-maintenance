---
feature: 008-assets
title: "Assets (Slice 1b) -- Implementation Plan"
created_at: "2026-07-12"
---

# Implementation Plan: Assets (Slice 1b)

**Branch**: `008-assets-WP##` | **Spec**: [spec.md](spec.md) | **Issue**: #79

## Summary

New Asset aggregate mirroring the Property/JobDefinition patterns through
all layers, an optional `assetId` threaded through Job and JobDefinition
(inherited on generation), and frontend surfaces: Assets section on the
property page, asset detail page, asset dropdowns on the two create
forms. Obsolete-flag convention throughout -- no deletion.

## Technical Context

**Backend**: C# / .NET 9, Clean Architecture (Domain -> Application ->
Infrastructure -> API), MongoDB, MiniValidation, xUnit + Testcontainers
**Frontend**: Next.js 15 App Router, Server Components + client islands,
server actions with `revalidatePath`, Tailwind
**Testing**: unit + integration (backend), Jest (components), Playwright
e2e in CI
**Scale/Scope**: 1 new aggregate, 2 extended aggregates, ~4 new frontend
components, 1 new page route, new e2e suite

## Constitution Check

No violations. Follows every established pattern; introduces the
obsolete-flag convention (recorded app-wide decision, 2026-07-12).

## Architecture (mirrors existing aggregates)

### Domain (`HomeMaintenance.Domain/Assets/`)
- `Asset` aggregate: `Id`, `Owner`, `PropertyId`, `Name` (1-200),
  `Category?` (<=100), `Notes?` (<=2000), `IsObsolete` (default false).
  Methods: `Create`, `Rename`, `SetCategory`, `SetNotes`,
  `MarkObsolete`, `ClearObsolete` (or a single `SetObsolete(bool)`).
- `Job` + `JobDefinition`: add optional `AssetId` (string?, set at
  creation only). `Job.Create` gains an `assetId` parameter (same pattern
  as the existing `jobDefinitionId` parameter).

### Application
- `Assets/Commands`: CreateAsset, UpdateAsset (name/category/notes/
  isObsolete -- PATCH semantics like UpdateJobDefinition).
- `Assets/Queries`: GetAsset, ListAssets (by propertyId).
- `Assets/Dto`: AssetDto `{ id, propertyId, name, category, notes,
  isObsolete }`.
- `IAssetRepository` in Common/Interfaces (Get/Add/Update/ListByProperty).
- CreateJob / CreateJobDefinition commands: accept optional AssetId;
  validate the asset exists, belongs to the same owner AND property, and
  is not obsolete (FR-06/FR-08).
- `JobGenerationService` + `GenerateNextOccurrenceHandler`: pass
  `definition.AssetId` into `Job.Create` (FR-07).
- Jobs list query: support `assetId` filter (mirrors the existing
  `definitionId`/`propertyId` filters); JobDefinitions list query: same.

### Infrastructure
- `AssetRepository` (Mongo, `assets` collection) mirroring
  PropertyRepository; index on `(ownerId, propertyId)`.
- Job/JobDefinition Mongo documents: nullable `assetId` field --
  backward compatible, no migration needed (SC-04).

### API (`Endpoints/AssetEndpoints.cs`)
- `POST /api/assets` (CreateAssetApiRequest: propertyId, name,
  category?, notes?)
- `GET /api/assets?propertyId=` -> list
- `GET /api/assets/{id}` -> AssetDto
- `PATCH /api/assets/{id}` (UpdateAssetApiRequest: name?, category?,
  notes?, isObsolete?)
- Extend CreateJobRequest / CreateJobDefinitionApiRequest with
  `AssetId?`; extend Job/JobDefinition DTOs with `assetId`.
- Jobs + JobDefinitions GET lists accept `assetId` query param.
- Ownership: default-deny fallback policy already applies; cross-owner
  -> NotFoundError -> 404 (FR-10).

### Frontend
- `src/lib/api-client.ts`: AssetDto type + assets api (create, list,
  get, update); `assetId` added to job/definition types and create
  bodies; list functions accept assetId filter.
- `src/app/assets/actions.ts`: createAsset, updateAsset server actions
  (revalidatePath property + asset pages).
- Property page: new `AssetList` section (create form: name + category;
  cards link to `/assets/{id}`; "Obsolete" badge when flagged).
- `src/app/assets/[id]/page.tsx`: server component fetching asset +
  jobs(assetId=) + definitions(assetId=); `AssetHeader` client island
  (InlineEditableText name -- ariaLabel "Edit asset name"; category/notes
  edit; obsolete toggle button).
- `CreateJobForm` + `CreateJobDefinitionForm`: optional `<select>` of
  the property's non-obsolete assets (server component passes them down;
  no client fetch needed).
- Job/definition detail pages: show a small asset link when scoped.

## Obsolete Semantics (decision record)

Reversible flag. Excluded from create-form dropdowns; badge in lists;
detail page fully functional. Zero cascade: existing jobs stay
actionable, definitions keep generating (documented in spec assumptions).

## Suggested Work Packages (for /polaris.tasks)

| WP | Scope | Domain |
|----|-------|--------|
| WP01 | Backend: Asset aggregate end-to-end (domain, application, repo, endpoints) + unit/integration tests | backend-logic |
| WP02 | Backend: assetId on Job/JobDefinition + generation inheritance + list filters + validation + tests (depends WP01) | backend-logic |
| WP03 | Frontend: api-client, actions, Assets section, asset detail page, form dropdowns + component tests (depends WP02) | frontend-craft |
| WP04 | E2E suite `wp08-assets.spec.ts` (create/edit asset, scope job+definition, obsolete lifecycle) + helper `createAssetViaApi` (depends WP03) | testing-specialist |

## Definition of Done

- [ ] All spec FRs implemented; SC-01..SC-04 demonstrated
- [ ] dotnet test green (unit + integration incl. new Asset coverage)
- [ ] Frontend jest green; full Playwright suite green locally and in CI
- [ ] No hard-delete paths introduced
- [ ] PRs merged to main; issue #79 closed
