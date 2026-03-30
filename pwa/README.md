# Focus PWA

This folder contains a standalone Progressive Web App for managing a small task list with GitHub-backed sync.

## Features

- Explicit connection flow for repository settings + GitHub PAT
- Mobile-first list UI with optimistic local queueing
- Add task input with submit button
- Done toggle per task
- Inline edit by tapping task text
- Delete action with confirmation prompt
- Retryable sync status with last-sync diagnostics
- App-shell caching through service worker
- Install prompt support and Android fallback instructions

## Files

- `index.html` - app shell markup and install controls
- `styles.css` - mobile-first styling
- `app.js` - tiny bootstrap that starts the modular runtime
- `src/` - browser-native ES modules for auth, GitHub sync, settings, and todo state
- `sw.js` - app-shell service worker cache strategy
- `manifest.webmanifest` - PWA metadata (name, icons, theme/display settings)
- `icons/` - manifest icons for installability
- `build.ps1` - copies the static deployable app into `dist/`

## Build and deploy

No npm/node bundler is required; this app is static and uses browser-native ES modules.

Runtime host/repository settings are loaded from `runtime-config.js` at page load time so deployment target details can be adjusted without rebuilding app logic.

For full deployment/runbook details, see `docs/pwa-deployment.md`.

Optional static build output:

```powershell
pwsh -File pwa/build.ps1
```

This creates `pwa/dist/` with the deployable static files.

1. Serve the `pwa/` folder over HTTPS in production (service workers require secure context).
2. Ensure these files are deployed at the site root (or update relative URLs as needed):
   - `index.html`
   - `styles.css`
   - `app.js`
   - `sw.js`
   - `runtime-config.js`
   - `src/**/*.js`
   - `manifest.webmanifest`
   - `icons/icon.svg`
   - `icons/icon-maskable.svg`
3. Confirm `Content-Type` headers:
   - `.webmanifest` => `application/manifest+json`
   - `.js` => `text/javascript`
   - `.css` => `text/css`
   - `.svg` => `image/svg+xml`

### Local preview

From repository root:

```bash
python -m http.server 4173 --directory pwa
```

Open `http://localhost:4173`.

At first launch, the app will ask for:

1. Repository owner
2. Repository name
3. Branch
4. Folder path inside the repository
5. GitHub personal access token

This app does not implement GitHub OAuth; it uses a PAT stored in browser local storage.

## Validate installability in Chrome on Android

1. Deploy to an HTTPS URL.
2. On Android, open the URL in Chrome.
3. Verify app eligibility:
   - Manifest loads successfully.
   - Service worker is registered.
   - Manifest includes standard and maskable SVG icons.
4. Trigger install:
   - Use the in-app **Install app** button when shown, or
   - Use Chrome menu > **Install app** / **Add to Home screen**.
5. Confirm installed app launches in standalone display mode and works offline for app-shell assets.

> Note: Android validation requires a physical/emulated Android Chrome environment and cannot be fully executed in this repository's headless CLI environment.
