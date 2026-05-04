# Android PWA Parity TODO

Compared against the active PWA map runtime in `pwa/src/main.js`, `pwa/src/maps/model.js`, settings, attachment, routing, and sync modules.

## Missing Or Partial Android Features

- [x] Interactive node star/unstar with sibling reordering and queued sync.
- [x] Add child task action from the map editor.
- [x] Persistent quick-add note composer that stays open for rapid entry.
- [x] Rename map file when editing the root node title.
- [x] Collapse/expand controls for node branches, respecting stored `collapsed`.
- [x] Render inline color markup and clickable HTTP links in node text/path/task/map surfaces.
- [x] Related nodes UI for outgoing links and backlinks, with navigation to linked nodes.
- [x] Attachment list on selected/editable nodes.
- [x] Attachment upload from device files with PWA-compatible type and size validation.
- [x] Voice note recording and upload.
- [x] Attachment viewers for image, text/PDF-ish text, and audio files.
- [x] Attachment download/share actions.
- [x] Attachment delete from node/viewer, including remote file cleanup.
- [x] Clipboard image/text node behavior that opens the primary attachment viewer instead of text edit.
- [x] Map/node deletion cleanup that removes owned attachment files.
- [x] Unreadable map recovery section.
- [x] Raw unreadable-map viewer and download.
- [x] Local JSON repair editor for broken maps.
- [x] Reset local broken map state from GitHub.
- [x] Discard queued pending operations for blocked maps.
- [x] Manual conflict resolution modal with JSON diff and per-operation local/remote choice.
- [ ] Detailed sync status panel with last sync time, state, message, error, and retry affordances.
- [ ] Top-bar refresh from GitHub even when there are no pending local changes.
- [ ] Explicit GitHub access validation/revalidation flow.
- [ ] Clear saved token without clearing cached maps.
- [ ] Hard reset that discards local cache and queued changes, then reloads from GitHub.
- [ ] FAB side preference, using the existing Android `fabSide` preference.
- [ ] Native equivalent for PWA hash/deep-link map routes, selected-node routes, and history behavior.
- [ ] Native equivalent for PWA update-available notification.

## PWA-Specific Notes

- Service worker app-shell caching and browser install prompts are PWA/platform features, not direct Android TODOs unless a native update/install flow is desired.
- Android already has partial domain/storage support for attachments, related nodes, inline formatting, rename, sync metadata, and FAB side, but lacks user-facing parity for the items above.
