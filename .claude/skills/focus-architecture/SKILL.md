---
name: focus-architecture
description: Repo map and cross-client architecture of the Focus mind-map app (Console .NET, PWA, Android). Read this FIRST before exploring the repo — it replaces broad directory scans. Use when orienting in the codebase, deciding where a change belongs, or working across clients.
---

# Focus — repo architecture

Focus is a personal mind-map/task tool. Maps are JSON files named `<MapName>.json` stored in a `FocusMaps` folder. Three independent clients edit the same files:

| Client | Location | Stack | Storage/sync |
|---|---|---|---|
| Console app | `Focus/` (solution `Focus/Focus.sln`) | .NET 8, Newtonsoft.Json, Velopack updates | Local files + Git sync (`GitHelper`, Windows only) |
| PWA | `pwa/` | Static, no bundler, browser-native ES modules | GitHub Contents API (PAT) + localStorage cache + offline queue |
| Android | `android/` | Kotlin, Jetpack Compose, kotlinx.serialization | GitHub Contents API (PAT) + file cache + pending-op queue + WorkManager |

All three implement the same domain model, mutation semantics, conflict resolution, and llm-interop format. **A behavior change to map semantics usually needs to be mirrored in all three clients** (and the PWA is the reference implementation the Android client ports from).

## Shared concepts (one-line each; details in `focus-map-schema` skill)

- Map document: `{ rootNode: {...}, updatedAt }` camelCase JSON; legacy PascalCase accepted on read, canonical camelCase written.
- Node: nodeType (0 Text, 1 IdeaBag, 2 TextBlock), taskState (0 None, 1 Todo, 2 Doing, 3 Done), GUID `uniqueIdentifier`, `links` dict, `metadata` (createdAtUtc/updatedAtUtc/source/device/attachments).
- Conflict resolution: each client has a `MapConflictResolver` that structurally merges git conflict-marker documents.
- LLM interop: `@ai ` prompt task nodes + sidecar jobs under `FocusMaps/_llm/jobs/*.json`; CLI at `tools/focus-interop-cli.mjs`.
- Attachments: files referenced by node metadata, stored next to maps; voice recording supported (console uses bundled ffmpeg).

## Where things live

- `docs/` — authoritative design docs: `llm-interop.md`, `todos-schema.md`, `pwa-deployment.md`, `auth.md`, `voice-recording.md`, `android-native-implementation-tasks.md`, `focus-config.sample.json`.
- `tools/focus-interop-cli.mjs` — Node CLI for agents to claim/complete `@ai` jobs (test: `node --test tools/focus-interop.unit.test.mjs`).
- `.github/workflows/deploy-pwa-pages.yml` — only CI: deploys `pwa/` to GitHub Pages on push to main touching `pwa/**`.
- `Releases/`, `Focus/Focus/ReleaseAndUpload*.ps1` — console app Velopack packaging/upload (Windows/Linux/Mac, SFTP/FTPS via WinSCP or ssh).
- Root `*.log` files — historical build/test output, ignore them.
- `.artifacts/`, `.dotnet/`, `.vs/`, `Focus/*/bin|obj`, `pwa/dist` — generated; never search these (exclude from Grep/Glob).

## Per-client deep dives

Use the dedicated skills instead of re-reading code:
- `focus-console-dev` — console app layout, command system, build/test/E2E harness.
- `focus-pwa-dev` — PWA module layout, main.js structure, offline queue, SW cache rules, tests.
- `focus-android-dev` — Android layout, ViewModel/sync coordinator, build/test with JAVA_HOME.
- `focus-map-schema` — exact JSON schema, mutation/merge rules, llm-interop file formats.

## User config (console)

`~/focus-config.json`: `dataFolder`, `gitRepository`, `voiceRecorder`, `translations` (e.g. Georgian ka-GE keyboard → command alphabet). Maps live in `<dataFolder>/FocusMaps`.
