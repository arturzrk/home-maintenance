# Tasks: 008-assets

## Subtask List

| ID | WP | Description | Parallel |
|----|----|-------------|----------|
| T001 | WP01 | Domain: Asset aggregate + unit tests | |
| T002 | WP01 | Application: AssetDto, Create/Update commands, Get/List queries, IAssetRepository | |
| T003 | WP01 | Infrastructure: Mongo AssetRepository + DI registration | |
| T004 | WP01 | API: AssetEndpoints (POST/GET list/GET id/PATCH) + Program.cs mapping | |
| T005 | WP01 | Integration tests: CRUD, ownership 404, validation | |
| T006 | WP02 | Domain: optional AssetId on Job + JobDefinition (Create signatures) | |
| T007 | WP02 | Application: CreateJob/CreateJobDefinition accept + validate assetId | |
| T008 | WP02 | Generation inheritance: inline horizon + generate-next copy definition.AssetId | |
| T009 | WP02 | assetId list filters (jobs + definitions) and DTO exposure | |
| T010 | WP02 | Tests: validation matrix, inheritance, filters | |
| T011 | WP03 | api-client: AssetDto, assets api, assetId on job/definition types + filters | |
| T012 | WP03 | Server actions: createAsset, updateAsset | |
| T013 | WP03 | Property page: Assets section (create form, cards, obsolete badge) | |
| T014 | WP03 | Asset detail page: AssetHeader (inline name, category/notes, obsolete toggle) + work lists | |
| T015 | WP03 | Asset dropdowns on CreateJobForm + CreateJobDefinitionForm; asset link on detail pages | |
| T016 | WP03 | Component tests (jest) for new components | |
| T017 | WP04 | e2e helper createAssetViaApi | |
| T018 | WP04 | WP08-1: create asset via UI -> appears in Assets section | [P] |
| T019 | WP04 | WP08-2: asset detail shows fields; inline rename works | [P] |
| T020 | WP04 | WP08-3: job scoped to asset appears on asset detail page | [P] |
| T021 | WP04 | WP08-4: definition scoped to asset -> generated job inherits asset | [P] |
| T022 | WP04 | WP08-5: obsolete lifecycle (badge, excluded from dropdowns, work intact, reversible) | [P] |
| T023 | WP04 | WP08-6: unscoped work unaffected (no asset dropdown selection -> no asset link) | [P] |

## Work Packages

### WP01 --- Backend: Asset aggregate end-to-end
Domain + Application + Infrastructure + API for the Asset aggregate,
mirroring Property/JobDefinition patterns. Subtasks T001-T005. No deps.

### WP02 --- Backend: assetId threading (depends WP01)
Optional AssetId on Job/JobDefinition with same-property/non-obsolete
validation, generation inheritance, list filters, DTO exposure.
Subtasks T006-T010.

### WP03 --- Frontend: assets UI (depends WP02)
api-client + actions + Assets section + asset detail page + create-form
dropdowns + component tests. Subtasks T011-T016.

### WP04 --- E2E suite (depends WP03)
`frontend/e2e/wp08-assets.spec.ts` (6 tests) + `createAssetViaApi`
helper. Subtasks T017-T023.

## Parallelization

Strictly sequential chain: WP01 -> WP02 -> WP03 -> WP04 (each consumes
the previous layer's surface). Within WP04 the 6 tests are independent.

## MVP Scope

All four WPs; the feature is not user-visible until WP03, not gated
until WP04.

## Next Commands

```bash
polaris implement WP01
polaris implement WP02 --base WP01
polaris implement WP03 --base WP02
polaris implement WP04 --base WP03
```
