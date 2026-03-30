# PWA QA Checklist

Use this checklist to validate the PWA end-to-end behavior in realistic device/network conditions.

## 1) First login

**Repro steps**
1. Open the app in a fresh browser profile (or clear local storage/session state first).
2. Enter a valid token in the token entry screen.
3. Submit the token.

**Expected outcomes**
- Login succeeds and routes to the todos screen.
- Todo list loads from remote storage without errors.
- No stale/invalid token warning is displayed.
- Subsequent refresh keeps the authenticated state.

## 2) Add/edit/toggle/delete flow

**Repro steps**
1. Add a new todo item with non-empty text.
2. Edit the todo text and save changes.
3. Toggle completion to done, then back to open.
4. Delete the todo.
5. Refresh the app and verify persisted state.

**Expected outcomes**
- Add creates an item with the submitted text.
- Edit updates the exact targeted item.
- Toggle updates the completion indicator immediately.
- Delete removes the item from visible list.
- Refresh preserves all committed changes.

## 3) Network loss behavior

**Repro steps**
1. Load the app with an active network connection.
2. Disable network (browser devtools offline mode or device airplane mode).
3. Perform read and write actions (list load, add/edit/toggle/delete).
4. Re-enable network and retry failed write actions.

**Expected outcomes**
- App shell still opens if previously cached by the service worker.
- Read/write failures show actionable error feedback.
- Failed writes are not silently dropped.
- After reconnect, retry succeeds and state becomes consistent.

## 4) Token invalidation handling

**Repro steps**
1. Authenticate with a token that initially works.
2. Invalidate/revoke the token from provider side (or substitute an expired token).
3. Trigger a sync operation (load or save).

**Expected outcomes**
- Sync fails with explicit authentication error state.
- User is prompted to re-authenticate or replace token.
- App does not continue attempting sync with a known-bad token without user feedback.
- After entering a valid token, sync resumes normally.

## 5) Install + launch from Android home screen

**Repro steps**
1. Open the app in Chrome on Android over HTTPS.
2. Trigger install using in-app Install button or Chrome menu (Install app/Add to Home screen).
3. Accept install prompt.
4. Launch from Android home screen icon.
5. Exercise basic todo operations while installed.

**Expected outcomes**
- Install prompt is available when installability criteria are met.
- App installs with expected name/icon.
- Launch opens in standalone mode (no browser URL bar chrome).
- Core todo interactions work in installed mode.
- Previously cached shell assets load when temporarily offline.
