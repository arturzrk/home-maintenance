---
work_package_id: WP01
title: "Backend: Asset aggregate end-to-end"
lane: "planned"
dependencies: []
base_branch: main
subtasks: [T001, T002, T003, T004, T005]
domain: backend-logic
test_status: required
test_file: backend/tests/HomeMaintenance.Integration.Tests/Assets/AssetEndpointsTests.cs
---

# WP01 - Backend: Asset aggregate end-to-end

## Objective

Introduce the Asset aggregate through all backend layers: domain entity,
application commands/queries, Mongo repository, and `/api/assets`
endpoints -- mirroring the Property and JobDefinition patterns exactly.
No Job/JobDefinition changes in this WP (that is WP02).

## Context

- Templates to mirror: `Domain/Properties/Property.cs` (simple aggregate),
  `Application/Properties/*` (commands/queries/DTO layout),
  `Infrastructure/Persistence/PropertyRepository.cs`,
  `API/Endpoints/PropertyEndpoints.cs` + `JobDefinitionEndpoints.cs`
  (PATCH semantics, MiniValidator, ToHttp/ToHttpCreated).
- Ownership: `IIdentityProvider.CurrentOwner`; not-found on cross-owner.
- Obsolete-flag convention: no delete endpoint, ever (FR-11).

## Subtasks

### T001 --- Domain aggregate + unit tests

`Domain/Assets/Asset.cs`: `Id`, `Owner`, `PropertyId`, `Name`,
`Category?`, `Notes?`, `IsObsolete` (default false).
Factory `Asset.Create(id, owner, propertyId, name, category, notes)`;
methods `Rename(name)`, `SetCategory(string?)`, `SetNotes(string?)`,
`SetObsolete(bool)`. Guard: name required 1-200; category <=100;
notes <=2000 (match validation limits used elsewhere via domain guard
clauses like Property/Job).
Unit tests in `Unit.Tests/Domain/Assets/AssetTests.cs`: creation,
invalid name/category/notes rejected, obsolete toggle round-trip.

### T002 --- Application layer

`Application/Assets/Dto/AssetDto.cs`:
`{ Id, PropertyId, Name, Category, Notes, IsObsolete }` + `ToDto()`
mapper (follow existing mapping style).
`Commands/CreateAsset.cs`: `CreateAssetCommand(PropertyId, Name,
Category?, Notes?)` -> validates the property exists and belongs to the
caller (NotFoundError otherwise) -> `Result<AssetDto>`.
`Commands/UpdateAsset.cs`: PATCH semantics like UpdateJobDefinition --
`UpdateAssetCommand(Id, Name?, Category?, Notes?, IsObsolete?)`; null
means "leave unchanged"; explicit clearing of category/notes follows the
same convention UpdateJobDefinition uses for its optional fields.
`Queries/GetAsset.cs`, `Queries/ListAssets.cs` (by PropertyId, property
ownership enforced).
`Common/Interfaces/IAssetRepository.cs`: `GetAsync(id, owner)`,
`AddAsync`, `UpdateAsync`, `ListByPropertyAsync(propertyId, owner)`.
Register handlers in `Application/DependencyInjection.cs`.

### T003 --- Infrastructure

`Infrastructure/Persistence/AssetRepository.cs` on an `assets`
collection, mirroring PropertyRepository (document mapping, owner
filtering). Index on `(ownerId, propertyId)` if the existing repos
create indexes; otherwise match whatever they do. Register in
`Infrastructure/DependencyInjection.cs`.

### T004 --- API endpoints

`API/Endpoints/AssetEndpoints.cs`, group `/api/assets`,
`RequireAuthorization()`:
- `POST /` -- `CreateAssetApiRequest(PropertyId [Required], Name
  [Required, 1-200], Category? [<=100], Notes? [<=2000])` -> 201 + dto
- `GET /?propertyId=` -> `{ assets: [...] }` or bare list -- match the
  shape convention of ListJobDefinitions (bare list) vs jobs
  (wrapper); pick the JobDefinitions bare-list style
- `GET /{id}` -> dto or 404
- `PATCH /{id}` -- `UpdateAssetApiRequest(Name?, Category?, Notes?,
  IsObsolete?)` -> dto
Map in Program.cs (`app.MapAssetEndpoints();`).

### T005 --- Integration tests

`Integration.Tests/Assets/AssetEndpointsTests.cs` mirroring
PropertyEndpointsTests: create -> 201 + fields; list filtered by
property; get by id; PATCH each field incl. isObsolete round-trip;
validation 400s (missing name, name >200); cross-owner GET/PATCH -> 404;
unauthenticated -> 401. Use `TestJson.Options` for deserialization.

## Definition of Done

- [ ] `dotnet test` green (new unit + integration tests included)
- [ ] `/api/assets` CRUD verified by integration tests; no DELETE route
- [ ] Handlers/repository registered; API boots (existing tests prove it)
- [ ] No Job/JobDefinition changes

## Risks

- **PATCH clear semantics** for category/notes: follow whatever
  UpdateJobDefinition does for optional string fields; document the
  choice in the command's XML doc comment.

## Run Command

```bash
polaris implement WP01
```
