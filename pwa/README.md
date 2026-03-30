# Focus PWA

This folder contains a standalone Progressive Web App for managing a small task list.

## Features

- Mobile-first list UI
- Add task input with submit button
- Done toggle per task
- Inline edit by tapping task text
- Delete action with confirmation prompt
- App-shell caching through service worker
- Install prompt support and Android fallback instructions

## Files

- `index.html` - app shell markup and install controls
- `styles.css` - mobile-first styling
- `app.js` - task behavior, local storage persistence, install prompt handling, service worker registration
- `sw.js` - app-shell service worker cache strategy
- `manifest.webmanifest` - PWA metadata (name, icons, theme/display settings)
- `icons/` - manifest icons for installability

## Build and deploy

No bundler is required; this app is static.

1. Serve the `pwa/` folder over HTTPS in production (service workers require secure context).
2. Ensure these files are deployed at the site root (or update relative URLs as needed):
   - `index.html`
   - `styles.css`
   - `app.js`
   - `sw.js`
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
