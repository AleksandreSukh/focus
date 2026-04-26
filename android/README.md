# Focus Android

This folder starts the native Android Kotlin implementation that will replace the browser-specific PWA runtime with platform-native equivalents.

## Current implementation slice

- Android Gradle project and Compose entry point.
- Focus map JSON model with legacy PascalCase read compatibility and canonical camelCase writes.
- Domain mutation engine for node text edits, task state changes, hide-done flags, child note/task creation, node deletion, and attachment metadata changes.
- Query helpers for map summaries, task entries, visible children, and related-node backlinks/outgoing links.
- Git conflict marker detection and structural auto-resolution for compatible Focus map documents.
- GitHub Contents API client/provider boundaries with SHA-based optimistic writes.
- Secure token store using Android Keystore-backed encrypted preferences.
- DataStore-backed settings/theme/FAB/sync metadata store.
- App-private file-backed map cache/queue store plus in-memory test implementation; Room can still replace this if queryable storage becomes necessary.
- Workspace sync coordinator for optimistic local mutations, persisted pending operations, and ordered replay through the GitHub-backed service.
- WorkManager background sync for queued node mutations with a connected-network constraint, plus manual pending-sync from the status chip.
- Application container for constructing native stores and GitHub-backed services.
- Compose screens for connection, map list, task list, map editing, task state changes, node editing, child note/task creation, hide-done toggles, map creation, and map deletion.

## Platform replacements for PWA APIs

- Browser `localStorage`: Android DataStore for settings/theme/sync metadata, Room for cached snapshots and pending operations, encrypted preferences for PATs.
- Hash/history routes: Navigation Compose routes and Android back stack.
- Service worker shell cache: native app package plus Room/file cache for offline map data.
- Blob/FileReader/object URLs/download anchors: content resolver, Storage Access Framework, share intents, streamed OkHttp bodies, and temp files.
- Install/update prompt: native launcher/adaptive icon and distribution-specific update flow.

## Build

The project was verified with the Gradle wrapper after setting `JAVA_HOME` to `C:\Program Files\Java\jdk-26.0.1`.

Expected local commands:

```powershell
cd android
$env:JAVA_HOME='C:\Program Files\Java\jdk-26.0.1'
$env:Path="$env:JAVA_HOME\bin;$env:Path"
.\gradlew.bat --no-problems-report test
.\gradlew.bat --no-problems-report assembleDebug
```
