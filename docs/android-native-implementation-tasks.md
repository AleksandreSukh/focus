# Android Native Implementation Tasks

This tracks the remaining work after the initial Kotlin scaffold in `android/`.

## Completed Slices

- Compose screens are wired to `FocusViewModel`, encrypted token storage, file-backed workspace cache, GitHub loading, and the optimistic pending operation queue.
- Map editing now supports task state changes, node text edits, add child note, add child task, delete node, hide/show done tasks, create map, and delete map.
- Pending node mutations are scheduled through WorkManager with a connected-network constraint, rescheduled when cached pending work is observed, can be manually retried from the status chip, and stay in the local workspace cache until synced.
- DataStore preference flows now use `distinctUntilChanged` so sync status writes do not rebind workspace observers.

## Storage And Sync

- Decide whether the file-backed `FileFocusLocalStore` is sufficient or replace it with Room entities for snapshots, pending operations, unreadable maps, and blocked pending maps.
- Expand WorkManager queue processing with visible retry status, cancellation, and blocked-operation handling.

## UI Parity

- Add attachment viewer, repair, and conflict dialogs.
- Add subtree navigation and deep links equivalent to PWA `#map/<path>?node=<id>`.
- Add left/right FAB preference, dark theme, status dialog, settings dialog, and hard reset.

## GitHub And Attachments

- Add repository/branch validation flow to the connection screen.
- Upload attachments with streamed base64 conversion and file picker integration.
- Add image zoom/pan, text preview, PDF/external open, share/download, and delete confirmation.
- Add missing-attachment tolerance on delete, matching the PWA.

## Conflict And Repair

- Expand resolver tests to cover newer scalar fields, links, attachments, hide-done flags, and timestamp fallback parity with `pwa/src/maps/mapConflictResolver.js`.
- Surface unreadable maps in UI as invalid JSON, merge conflict, auto-resolve failure, or unknown.
- Implement local repair editor/import/export, reset to GitHub, retry, discard queued operations, and manual take-local/take-remote conflict resolution.

## Verification

- Expand JVM tests for model normalization, mutations, related nodes, inline formatting, commit messages, and conflict resolution.
- Add fake GitHub provider integration tests for stale SHA retries, queue pause/resume, and attachment cleanup.
- Add Compose UI tests for first run, maps/tasks navigation, map editing, attachment flows, repair, conflict resolution, settings, and hard reset.
