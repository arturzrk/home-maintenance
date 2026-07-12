---
feature: 008-assets
title: "Assets (Slice 1b)"
status: draft
created_at: "2026-07-12"
---

# Assets (Slice 1b)

## Objective

Let a homeowner model the physical things they maintain (boiler, roof,
lawn mower) as **Assets** on a property, and scope maintenance work --
one-shot jobs and recurring definitions -- to them. Assets follow the new
app-wide **obsolete-flag** convention: they are soft-retired, never
deleted, so the full maintenance history of a replaced boiler stays
readable forever. (GitHub issue #79, deferred from Slice 1 / #1.)

## Actors

- **Homeowner** -- an authenticated user who owns properties, assets, and
  their maintenance work.

## User Scenarios

### US1 -- Create an asset
On a property's detail page, the homeowner adds an asset by name (e.g.
"Boiler"), optionally with a category and notes. It appears in the
property's Assets list.

### US2 -- View an asset
Clicking an asset opens its detail page: name, category, notes, and two
work lists -- the jobs and the recurring definitions scoped to it.

### US3 -- Rename / edit an asset
The homeowner renames an asset inline and edits its category/notes. The
changes are visible without a page reload.

### US4 -- Scope work to an asset
When creating a job or a recurring definition on the property page, the
homeowner can pick one of the property's active assets from a dropdown
(or leave it unscoped). The asset's detail page then lists that work, and
jobs generated from an asset-scoped definition inherit the asset.

### US5 -- Mark an asset obsolete
The homeowner marks a replaced asset obsolete. It disappears from the
asset dropdowns for new work and is visually distinguished in lists, but
its detail page, its history, and everything referencing it stay intact.
The flag can be cleared to reactivate the asset.

### US6 -- Obsolete asset does not disturb existing work
Work already scoped to a now-obsolete asset behaves exactly as before:
jobs stay actionable and recurring definitions keep generating until the
homeowner changes them.

## Functional Requirements

| ID | Requirement |
|----|-------------|
| FR-01 | An asset belongs to exactly one property and one owner; a property can have many assets. |
| FR-02 | An asset has a required name (1-200 chars), optional category (free text, up to 100 chars), optional notes (up to 2000 chars), and an isObsolete flag (default false). |
| FR-03 | Assets are created from and listed on the property detail page. |
| FR-04 | An asset detail page shows its fields and the jobs and recurring definitions scoped to it. |
| FR-05 | Asset name, category, and notes are editable; name inline-editable like other entities. |
| FR-06 | Jobs and job definitions accept an optional asset reference at creation; the referenced asset must belong to the same property. |
| FR-07 | Jobs generated from an asset-scoped definition carry the same asset reference. |
| FR-08 | The isObsolete flag can be set and cleared. Obsolete assets are excluded from asset pickers for new work but remain listed (visually distinguished) and fully viewable. |
| FR-09 | Marking an asset obsolete changes nothing about existing jobs or definitions that reference it. |
| FR-10 | Ownership rules match the rest of the app: default-deny, cross-owner access returns not-found. |
| FR-11 | No hard deletion of assets. |

## Success Criteria

| ID | Criterion |
|----|-----------|
| SC-01 | A homeowner can create an asset, scope a job and a definition to it, and see both on the asset detail page -- entirely through the UI. |
| SC-02 | An obsolete asset no longer appears in the create-form dropdowns, while its existing work remains visible and functional. |
| SC-03 | Backend unit + integration tests cover the aggregate, endpoints, and ownership; e2e tests cover the UI scenarios; all run green in CI. |
| SC-04 | Existing data is unaffected: jobs/definitions without an asset keep working unchanged (assetId is optional everywhere). |

## Key Entities

- **Asset** -- `{ id, ownerId, propertyId, name, category?, notes?, isObsolete }`
- **Job** (extended) -- gains optional `assetId`
- **JobDefinition** (extended) -- gains optional `assetId`; generation copies it to created jobs

## Assumptions

- Obsolete-flag semantics (per app-wide convention agreed 2026-07-12):
  reversible; excluded from pickers for new work; no cascade effects --
  in particular, definitions scoped to an obsolete asset keep generating
  until the user edits them. Revisit if this surprises users.
- Asset categories are free text in this slice; a curated taxonomy can
  come later without migration pain.
- Existing job/definition edit forms do not gain asset re-assignment in
  this slice -- the asset is chosen at creation time only.
- Full local stack + established CI pipeline; estimation skipped.

## Out of Scope

- Hard deletion of any entity.
- Serial numbers, install dates, warranties, photos, documents.
- Moving an asset to a different property.
- Re-assigning existing jobs/definitions to a different asset.
- Curated category taxonomy or category-based filtering.
