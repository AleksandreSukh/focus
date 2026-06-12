---
name: focus-map-schema
description: Exact JSON schema of Focus mind-map files, node/task enums, metadata, links, attachments, llm-interop job format, and cross-client merge/conflict rules. Use when reading/writing map JSON, changing the domain model, working on sync/merge, or implementing @ai / llm-interop features.
---

# Focus map JSON schema & sync semantics

## Map document

File: `<FocusMaps>/<MapName>.json`. Canonical serialization is **camelCase, indented**; all clients accept legacy PascalCase keys on read (Android lowercases the first char of every key; console uses Newtonsoft CamelCasePropertyNamesContractResolver).

```json
{
  "rootNode": { ...node },
  "updatedAt": "2026-05-18T08:00:00Z"
}
```

Timestamps: canonical format `yyyy-MM-ddTHH:mm:ssZ` (UTC, second precision, no millis) — defined in `Focus/Focus/Domain/NodeMetadata.cs` (`TimestampFormat`) and mirrored in PWA/Android normalizers.

## Node

```json
{
  "nodeType": 0,
  "uniqueIdentifier": "guid",
  "name": "text (multiline allowed for TextBlock)",
  "children": [ ...nodes ],
  "links": { "<target-guid>": { "id": "guid", "relationType": 0, "metadata": null } },
  "number": 1,
  "starred": false,
  "collapsed": false,
  "hideDoneTasks": false,
  "hideDoneTasksExplicit": true,
  "taskState": 0,
  "metadata": {
    "createdAtUtc": "...Z", "updatedAtUtc": "...Z",
    "source": "manual", "device": "MACHINENAME",
    "attachments": [
      { "id": "guid", "relativePath": "file.png", "mediaType": "image/png",
        "displayName": "file.png", "createdAtUtc": "...Z" }
    ]
  }
}
```

- `nodeType`: 0 TextItem, 1 IdeaBagItem, 2 TextBlockItem (multiline answer/blocks).
- `taskState`: 0 None, 1 Todo (`*`), 2 Doing, 3 Done.
- `number` is 1-based per numbering group: IdeaBag items number separately from Text/TextBlock items; renumbered on delete.
- `hideDoneTasksExplicit` is nullable; only serialized when true (NullValueHandling.Ignore). `hideDoneTasks=true` without explicit flag is a legacy implicit override.
- `metadata.source` values: `manual`, `clipboard-text`, `clipboard-image`, `legacy-import`, `llm:<Agent>` (e.g. `llm:Codex`).
- Normalization (all clients): missing GUIDs regenerated, control chars stripped from `name` (keep \r\n\t), timestamps coerced to canonical format, attachment `relativePath` reduced to file name, numbers recomputed.
- Every mutation "touches" `metadata.updatedAtUtc` of the node and ancestors' map `updatedAt`.

Authoritative implementations: `Focus/Focus/Domain/Node.cs` + `MindMap.cs` (console), `pwa/src/maps/model.js` (PWA), `android/.../domain/model/MindMapJson.kt` + `Node.kt` (Android).

## Conflict resolution (git conflict markers)

When a map file contains `<<<<<<<`/`=======`/`>>>>>>>`, each client parses both sides as documents and structurally merges by node GUID. Implementations: `Focus/Focus/Domain/MapConflictResolver.cs`, `pwa/src/maps/mapConflictResolver.js`, `android/.../domain/maps/MapConflictResolver.kt`. Rules: union nodes by id; field winner = newer `metadata.updatedAtUtc`; unresolvable docs surface as "unreadable map" (PWA/Android offer repair UI; console throws `MapConflictAutoResolveException` → manual git merge recovery in `GitHelper`).

## Optimistic concurrency (PWA/Android vs GitHub)

GitHub Contents API with SHA-based compare-and-swap: load keeps blob `sha` as `revision`; PUT sends sha; on 409/422 re-fetch, structural merge, retry once; otherwise pending operations get blocked and surface as "blocked pending map" with discard/resolve UI. Mutations are queued locally (optimistic) and replayed in order.

## Todos MVP (legacy, separate from maps)

`docs/todos-schema.md` defines the older `{version, items:[{id,text,completed,deleted,updatedAt}]}` todos file with LWW merge by `updatedAt` and tombstones. The `pwa/src/todos/` + `*.ts` files belong to that earlier MVP; current development centers on mind maps.

## LLM interop (`docs/llm-interop.md`)

- Prompt = task node whose `name` starts with `@ai `. On completion: prompt marked Done, answer appended as child TextBlock (`nodeType: 2`) with `metadata.source: "llm:<Agent>"`.
- Sidecar jobs: `FocusMaps/_llm/jobs/<jobId>.json`:
  `{ version:1, id, status: pending|claimed|completed|failed, mode, mapFilePath, nodeId, prompt, createdAt, updatedAt }`.
- CLI: `node tools/focus-interop <cmd>` — `context --map --node`, `jobs list|claim|complete|fail`. Resolves maps dir from `--maps-dir`, then `~/focus-config.json`, then `./FocusMaps`.
- Console commands: `ai <prompt|child>`, `aijobs [run [jobId]]` (runs local `codex exec`, read-only, answer via `--output-last-message`). Focus is the only writer of map/job files.
