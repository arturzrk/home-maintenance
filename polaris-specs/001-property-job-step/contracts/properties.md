# Contract: Properties

Base: `/api/properties`

## POST /api/properties

Create a Property owned by the caller.

**Request body**:
```json
{
  "name": "Main House"
}
```

| Field | Type | Required | Constraints |
|---|---|---|---|
| `name` | string | yes | 1..100 chars, trimmed |

**201 Created**:
```json
{
  "id": "01J9P7MF1XAN70VW8RHP3Y6E13",
  "name": "Main House"
}
```
Response header `Location: /api/properties/{id}`.

**400 ValidationError** (empty or oversized name).
**401 Unauthorized** (no/invalid token).

**Audit**: `property.created` with `target=property:{id}`, payload `{name}`.

## GET /api/properties

List Properties owned by the caller, alphabetical by name.

**Query**: none.

**200 OK**:
```json
{
  "properties": [
    { "id": "...", "name": "Beach Cabin" },
    { "id": "...", "name": "Main House" }
  ]
}
```
Returns `{ "properties": [] }` for users with none.

**401 Unauthorized**.

## GET /api/properties/{id}

Read a single Property by id.

**200 OK**:
```json
{
  "id": "...",
  "name": "Main House"
}
```

**404 NotFound**: id is unknown OR is owned by another user.

## PATCH /api/properties/{id}

Rename a Property.

**Request body**:
```json
{
  "name": "Main Residence"
}
```

| Field | Type | Required | Constraints |
|---|---|---|---|
| `name` | string | yes | 1..100 chars, trimmed |

**200 OK**: returns the updated Property representation.
**400 ValidationError**.
**404 NotFound** (unknown or not owned).

**Audit**: `property.renamed` with `target=property:{id}`, payload
`{old_name, new_name}`.
