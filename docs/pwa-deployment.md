# PWA deployment and runtime configuration

This repository's web client (`pwa/`) is deployed as a static site. The deployment target is **GitHub Pages**.

## 1) Host target selection

- **Selected host:** GitHub Pages (first-party for GitHub repos, no app server required).
- **Why this target:** the PWA is static (`index.html`, CSS, JS, manifest, service worker), so Pages can host it directly.
- **Equivalent hosts:** Cloudflare Pages, Netlify, Vercel static output mode, or any static HTTPS host.

## 2) Runtime config for repo owner/name/branch/path

Runtime config is in `pwa/runtime-config.js` and loaded by `pwa/index.html` before `app.js`.

Configuration object:

```js
window.__FOCUS_RUNTIME_CONFIG__ = {
  host: 'github-pages',
  repoOwner: '',
  repoName: '<repo-name>',
  repoBranch: 'main',
  repoPath: '/',
  auth: {
    tokenStorageKey: 'focus_runtime_token',
    tokenSource: 'runtime-only',
  },
};
```

Notes:

- `repoOwner`, `repoName`, `repoBranch`, and `repoPath` are runtime settings and can be changed without rebuilding app logic.
- `repoName` is inferred from URL path by default if possible.
- **Do not put access tokens in this file.** It is public client-side code.
- User token must be entered/provided at runtime only, then kept in runtime storage (for example session/local storage) according to your auth UX.

## 3) Build pipeline that outputs static assets only

Workflow file: `.github/workflows/deploy-pwa-pages.yml`

Pipeline behavior:

1. Triggers on pushes to `main` affecting `pwa/**` (or manual dispatch).
2. Copies `pwa/` into a `dist/` directory.
3. Validates static entry files exist (`index.html`, `app.js`, `runtime-config.js`).
4. Uploads `dist/` as Pages artifact.
5. Deploys artifact with `actions/deploy-pages`.

No server build and no secret injection occur in the build.

## 4) Post-deploy smoke test checklist

After each deploy, validate in the deployed URL:

- [ ] **App loads**
  - [ ] `index.html` renders and task list UI appears.
  - [ ] Console has no blocking JS errors.
  - [ ] Service worker registers successfully.
- [ ] **Auth works**
  - [ ] User can provide token at runtime (not bundled in source, not in workflow secrets for client injection).
  - [ ] Authenticated requests succeed with that runtime token.
  - [ ] Reload behavior matches expected token persistence policy.
- [ ] **File read/write commits appear in repo history**
  - [ ] App can read target repo path configured by owner/name/branch/path.
  - [ ] Write action (create/update task file) succeeds.
  - [ ] Commit is visible in GitHub commit history on configured branch.
  - [ ] Commit metadata/message matches expected format.

## GitHub Pages setup checklist

In repository settings:

1. Go to **Settings → Pages**.
2. Set **Source** to **GitHub Actions**.
3. Ensure default deployment branch is `main` (or adjust workflow triggers + runtime config branch).
4. Run workflow **Deploy PWA to GitHub Pages** once.
