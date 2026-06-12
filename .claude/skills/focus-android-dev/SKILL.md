---
name: focus-android-dev
description: Focus Android (Kotlin/Compose) client development — package layout, ViewModel/sync coordinator pattern, GitHub provider, local stores, build & test commands with JAVA_HOME setup. Use for any change under android/.
---

# Focus Android client development

Native Kotlin port of the PWA (PWA is the reference implementation). Single module `android/app`, package `com.systemssanity.focus`. Compose + Material3, kotlinx.serialization, OkHttp, DataStore, WorkManager, Coil. minSdk 26, target/compile 35, JVM 17. No Hilt — manual DI via `di/AppContainer.kt`.

## Build / test

```powershell
cd android
$env:JAVA_HOME='C:\Program Files\Java\jdk-26.0.1'
$env:Path="$env:JAVA_HOME\bin;$env:Path"
.\gradlew.bat --no-problems-report test            # JVM unit tests (JUnit4 + kotlin.test)
.\gradlew.bat --no-problems-report assembleDebug
```

Optional gradle property `focus.updateManifestUrl` → `BuildConfig.UPDATE_MANIFEST_URL` (in-app update check, `data/appupdates/AppUpdateChecker.kt`).

## Package layout (`app/src/main/java/com/systemssanity/focus/`)

- `domain/model/` — `MindMapDocument` (rootNode+updatedAt), `Node`, `NodeMetadata`, `NodeType`, `TaskState`, `MindMapJson` (parse: lowercase-first-char key canonicalization for legacy PascalCase; serialize: canonical camelCase + trailing newline; normalize: GUIDs, timestamps to ISO seconds, sanitize text), `ClockProvider`.
- `domain/maps/` — pure logic ported from `pwa/src/maps/`: `MapMutation` (sealed ops) + `MapMutationEngine.apply()` (returns `Applied`/`Rejected`), `MapQueries` (summaries/tasks/visible children), `MapConflictResolver`, `RelatedNodes`, `InlineFormatter`, `CommitMessages`, `UnreadableMaps`, `LocalMapRepairs`, `PendingConflicts`, `BlockedPendingMaps`, `ConflictDiffs`, `Attachment{Uploads,Exports,Viewers}`, `MapFilePaths`, `VoiceNotes`.
- `domain/sync/` — `MindMapService` (snapshot cache), `MindMapRepository` (error mapping), `WorkspaceSyncCoordinator` (THE sync brain: loadWorkspace merges remote + pending ops, enqueueMutation applies optimistically via MapMutationEngine and persists pending op, processPendingOperations replays FIFO and surfaces unreadable/blocked/conflict entries), `PendingMapOperations`, `FocusSyncWorker` (WorkManager, network-constrained background replay).
- `data/github/` — `GitHubContentClient` (Contents API, SHA CAS writes), `GitHubMindMapProvider`, `GitHubAccessValidation`, `GitHubModels`.
- `data/local/` — `FocusLocalStore` interface + `FileFocusLocalStore` (app-private file JSON cache of workspace+pending queue, keyed by repo `scope`), `PreferencesStore` (DataStore: settings/theme/FAB side/sync metadata), `SecureTokenStore` (Keystore-encrypted prefs for PAT), `LocalModels`.
- `di/AppContainer.kt` — constructs stores; `createMindMapService(settings, token)`, `createWorkspaceSyncCoordinator(...)`.
- `ui/` — `FocusViewModel.kt` (~2k lines: single `FocusUiState` data class via `mutableStateOf`, all actions as fun per use case), `FocusApp.kt` (~4k lines: ALL composable screens — connection, map list, map tree, tasks, modals/sheets), `FocusRoutes.kt` (native route model `Maps|Tasks|Map(filePath,nodeId)` + back/forward `NativeRouteHistory`, parses shared `#/...` URI form), `FocusTheme.kt`, `FocusInlineText.kt`, `AndroidVoiceNoteRecorder.kt`.

## Conventions

1. Port semantics from the PWA module of the same name; keep behavior identical (same commit messages, same merge rules, same route strings). When fixing a sync/domain bug here, check `pwa/src/maps/` and console `Domain/` for the same bug.
2. All mutations go through `MapMutation` + `WorkspaceSyncCoordinator.enqueueMutation` — never write GitHub directly from UI; ViewModel calls coordinator then refreshes `uiState`.
3. UI state is one immutable `FocusUiState` copied via `uiState = uiState.copy(...)`; nested feature states (`AttachmentViewerUiState`, `ConflictResolutionUiState`, `LocalMapRepairUiState`, `AppUpdateUiState`...).
4. Tests are plain JVM tests under `app/src/test/...` mirroring package structure; UI logic tested via state/route classes (`FocusedMapViewTest`, `FocusRoutesTest`) — no instrumentation tests in CI.
5. Error/edge flows mirror PWA: unreadable maps, blocked pending ops (discard/resolve), pending-conflict resolver with per-operation choices.

`docs/android-native-implementation-tasks.md` tracks the porting backlog/status.
