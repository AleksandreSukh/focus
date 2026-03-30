# Todos JSON Schema, Versioning, and Conflict Resolution

## 1) JSON schema fields and constraints

The todos data file is a JSON object with a top-level `version` and an `items` array.

### Canonical schema (Draft 2020-12)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://example.com/schemas/todos.json",
  "title": "TodosFile",
  "type": "object",
  "additionalProperties": false,
  "required": ["version", "items"],
  "properties": {
    "version": {
      "type": "integer",
      "minimum": 1,
      "description": "Monotonic schema/data-format version for migrations."
    },
    "items": {
      "type": "array",
      "items": { "$ref": "#/$defs/todoItem" }
    }
  },
  "$defs": {
    "todoItem": {
      "type": "object",
      "additionalProperties": false,
      "required": ["id", "text", "completed", "updatedAt"],
      "properties": {
        "id": {
          "type": "string",
          "minLength": 1,
          "maxLength": 128,
          "description": "Stable unique identifier (UUID/ULID/etc.)."
        },
        "text": {
          "type": "string",
          "minLength": 1,
          "maxLength": 500,
          "description": "Non-empty todo text with a bounded maximum length."
        },
        "completed": {
          "type": "boolean"
        },
        "deleted": {
          "type": "boolean",
          "default": false,
          "description": "Soft-delete tombstone flag for deterministic merges."
        },
        "updatedAt": {
          "type": "string",
          "format": "date-time",
          "description": "RFC 3339 timestamp in UTC; used for conflict resolution."
        },
        "createdAt": {
          "type": "string",
          "format": "date-time"
        }
      }
    }
  }
}
```

### Required constraints summary

- `text` **must be non-empty** (`minLength: 1`).
- `text` has a **maximum length** (`maxLength: 500` in this spec; if product limits differ, keep schema + UI validator aligned).
- `id` must be stable and non-empty.
- `updatedAt` must be RFC 3339 date-time and should always move forward on mutation.

---

## 2) Migration policy for `version`

Treat `version` as the persisted file format version.

- **Bump major file version** whenever a change is backward-incompatible (field rename/removal, semantic reinterpretation, new required fields without defaults).
- **Do not bump** for purely additive optional fields that older readers can safely ignore.
- Maintain deterministic, tested migrators: `vN -> vN+1`.
- On read:
  1. Parse file.
  2. If `version` is older, run sequential migrators to current version.
  3. Validate after each step (or at minimum final state).
  4. Persist upgraded document only after successful validation.
- On unknown future version (`file.version > app.supportedVersion`):
  - Fail safely in read-only mode and prompt user/app update.
- Migration functions must be **idempotent per source version** and produce canonical ordering/shape so retries are stable.

---

## 3) Optimistic concurrency flow

Use compare-and-swap semantics with a remote version token (e.g., ETag, generation, or opaque revision ID).

1. **GET file + token**
   - Fetch todos JSON and store server-provided token `remoteToken0`.
2. **Apply local mutation**
   - Mutate local in-memory model.
   - Update affected item's `updatedAt` (UTC now).
3. **PUT with `If-Match: remoteToken0`** (or equivalent token field)
   - If success: done.
4. **If conflict (HTTP 409/412)**
   - Re-fetch latest file + token `remoteToken1`.
   - Merge local pending change(s) with latest remote by `id` using deterministic rules below.
   - Retry one PUT with `If-Match: remoteToken1`.
5. **If second conflict fails**
   - Stop auto-retrying.
   - Show manual conflict resolution UI with:
     - local intent,
     - remote current item,
     - merged suggestion.

Pseudo-flow:

```text
GET -> (doc0, token0)
localDoc = mutate(doc0)
PUT(ifMatch=token0, body=localDoc)
  success => done
  conflict =>
    GET -> (doc1, token1)
    merged = merge(doc1, pendingLocalChanges)
    PUT(ifMatch=token1, body=merged)
      success => done
      conflict => showManualConflictUI()
```

---

## 4) Deterministic merge rules

Merge by `id` across local pending changes and latest remote.

### Rule set

1. **Identity / matching**
   - Items with same `id` are the same logical todo.
2. **Adds**
   - If `id` exists only on one side, include it.
3. **Toggle/delete precedence**
   - For `completed` toggles and `deleted` state, winner is the side with the **newer `updatedAt`**.
4. **Text edits**
   - `text` winner is the side with the **newer `updatedAt`**.
5. **Tie-breaker (equal timestamps)**
   - Use deterministic fallback: lexical compare of source marker (`remote` > `local`) or stable hash rule. Pick one policy and never vary.
6. **Physical removal**
   - Keep tombstones (`deleted: true`) during merge window to prevent resurrection; garbage-collect later via retention policy.

### Worked examples

Assume ISO timestamps in UTC.

#### Example A: Add vs add (different IDs)

- Local adds `{id:"a1", text:"Buy milk", updatedAt:"2026-03-30T10:00:00Z"}`
- Remote adds `{id:"b2", text:"Call mom", updatedAt:"2026-03-30T10:00:05Z"}`

Result: both items are present (IDs differ).

#### Example B: Edit text conflict (same ID)

Base item: `{id:"t1", text:"Plan trip", updatedAt:"2026-03-30T10:00:00Z"}`

- Local edit -> `text:"Plan Japan trip"`, `updatedAt:"2026-03-30T10:01:00Z"`
- Remote edit -> `text:"Plan summer trip"`, `updatedAt:"2026-03-30T10:01:05Z"`

Result: text becomes **"Plan summer trip"** (newer timestamp wins).

#### Example C: Toggle vs delete conflict (same ID)

Base item: `{id:"t2", completed:false, deleted:false, updatedAt:"2026-03-30T10:00:00Z"}`

- Local toggles complete -> `completed:true`, `updatedAt:"2026-03-30T10:02:00Z"`
- Remote deletes -> `deleted:true`, `updatedAt:"2026-03-30T10:02:03Z"`

Result: remote delete wins (`deleted:true`), because delete/toggle resolves by newest `updatedAt`.

#### Example D: Delete vs text edit conflict (same ID)

Base item: `{id:"t3", text:"Draft email", deleted:false, updatedAt:"2026-03-30T10:00:00Z"}`

- Local text edit -> `text:"Draft launch email"`, `updatedAt:"2026-03-30T10:03:00Z"`
- Remote delete -> `deleted:true`, `updatedAt:"2026-03-30T10:03:04Z"`

Result: keep tombstone (`deleted:true`) and retain remote-winning state; item is not resurrected by older text edit.
