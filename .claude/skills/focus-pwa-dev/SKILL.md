---
name: focus-pwa-dev
description: Focus PWA development — module layout, main.js render/state pattern, offline queue & GitHub sync, service-worker cache bump rule, runtime config, test/serve/deploy commands. Use for any change under pwa/.
---

# Focus PWA development

Static app, **no npm/node_modules/bundler** — browser-native ES modules. Entry: `index.html` → `runtime-config.js` (sets `window.__FOCUS_RUNTIME_CONFIG__`: host, repoOwner/Name/Branch/Path, storage keys) → `app.js` → `src/main.js` `bootstrapApp()`.

## Commands

```powershell
node --test "pwa/src/**/*.unit.test.js"   # unit tests (node:test; ~110 tests, sub-second)
pwsh -File pwa/serve-local.ps1            # local preview at http://localhost:4173 (SW-enabled; file:// unsupported)
pwsh -File pwa/build.ps1                  # optional: copy static files to pwa/dist
```

Deploy: push to `main` touching `pwa/**` triggers `.github/workflows/deploy-pwa-pages.yml` → GitHub Pages. No build step, no secrets; PAT is entered at runtime and kept in localStorage.

## Module layout (`pwa/src/`)

- `main.js` (~7.7k lines) — the entire UI controller: global `state` + `ui` objects, hash-route navigation (`#/maps`, `#/tasks`, `#/map/<path>` + modal overlays in history state), DOM event delegation (`handleDocumentClick/Submit/Change/Input/Keydown`), `enqueueOperation` → `processPendingOperations` sync loop, all `render*` functions (string-template HTML + `escapeHtml`, keyed render cache to avoid re-render). No framework.
- `maps/model.js` — pure domain: TASK_STATE/NODE_TYPE consts, parse/serialize/normalize document, `applyMapMutation`, task queries, hide-done visibility, badges. **Mirror of console `Domain/` and Android `MapMutationEngine`.**
- `maps/mindMapService.js` — snapshot cache per filePath; list/load/create/delete/mutate via repository.
- `maps/mindMapRepository.js` — wraps provider, maps thrown errors to `{ok:false,error:{code,retriable,...}}` results (`PERSISTENCE_ERROR`, `UNREADABLE_MAP`).
- `maps/githubMindMapProvider.js` — GitHub Contents API: list dir, base64 get/put with blob `sha` as revision (CAS), llm job files, attachments.
- `maps/offlineQueue.js` — builds pending operation objects (createMap/deleteMap/mutation...) and optimistic snapshots from them.
- `maps/localCache.js` — localStorage persistence of snapshots + pending queue (repo-scoped keys).
- `maps/mapConflictResolver.js` — git-conflict-marker structural merge; `maps/relatedNodes.js` — backlinks/outgoing links; `maps/routes.js` — route parse/build.
- `gitProvider/` — lower-level GitHub client + commit message builders (`commitMessages.js`) + `adapters/githubAdapter.js`; `syncMetadata.js` last-sync info.
- `auth/` — PAT validation/session; `settings/` — connection + settings screens, `repoSettings.js`; `navigation/history.js` — back/forward stacks; `formatting/inlineFormatter.js` — inline bold/italic/link markup; `attachments/` — media type detection, image preview, voice recorder (MediaRecorder); `llm/interop.js` — @ai prompt/job logic.
- `todos/` and the scattered `.ts` files — older todos-MVP artifacts, **not loaded by the app** (not in `sw.js` asset list); don't extend them. Always run tests with the explicit `"pwa/src/**/*.unit.test.js"` glob — a bare `node --test pwa/src/` fails because it also picks up the `.ts`/integration test files.

## Hard rules

1. **Service worker**: any added/renamed/removed runtime JS module MUST be reflected in `sw.js` `APP_SHELL_ASSETS`, and `CACHE_NAME` version (`focus-pwa-shell-vNN`) MUST be bumped on every deployable asset change, or clients keep stale code.
2. Result-object convention: repository/service layers never throw to UI; they return `{ok:true,value}` / `{ok:false,error:{code,message,retriable}}`.
3. Mutations are optimistic: apply to local snapshot + enqueue; `processPendingOperations` replays in order; CAS conflict → structural merge retry → else map becomes "blocked pending" with repair/discard UI (`renderNeedsRepairSection`, repair modal).
4. Unreadable maps (bad JSON/conflict markers) are first-class state: viewer/download/repair/reset-to-remote flows in main.js.
5. Keep domain logic out of main.js — put it in `maps/*.js` modules with a `*.unit.test.js` next to it (node:test, `describe/it` + `assert`). main.js itself has no tests; it's exercised manually/by QA checklist (`docs/pwa-qa-checklist.md`).
6. New UI state that should survive navigation belongs in the hash-route/overlay system (`navigationEntryFromHash`, `navigationOverlayFromActiveModal`), not ad-hoc globals.

## Theming/prefs

localStorage keys: `focus.pwa.theme`, `focus.pwa.fabSide`, plus runtime-config auth keys (`focus_runtime_token`, `focus_runtime_repo_settings`). Repo-scoped state keys are derived per owner/repo/branch/path (`switchRepoContext`).
