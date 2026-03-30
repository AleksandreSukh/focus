# PWA Authentication for Private Repository Access

## Token Type
Use a personal access token (PAT) with the minimum scopes required for private repository read access.

## Required Scopes
- `repo` (classic PAT): Required to read private repository metadata and contents.
- Fine-grained PAT alternative:
  - Repository access: select the target private repository.
  - Permissions:
    - Contents: **Read**
    - Metadata: **Read**

## Validation Flow
1. User enters token on the PWA token entry screen.
2. App performs a lightweight API probe with `Authorization: Bearer <token>`.
3. On success, token is persisted locally for the active browser profile.
4. On `401` or `403`, the UI shows a mapped, actionable error message.

## Local Storage and Security
The PWA stores the token in `localStorage` via `focus.pwa.auth.token`.

> Security note: `localStorage` is convenient but not encrypted at rest and is vulnerable to token exposure if XSS exists in the app. Use least-privilege tokens, avoid shared devices, and revoke tokens when no longer needed.

## Revoking a Local Session
Users can revoke the local session from Settings using **Revoke local session**, which clears the token from `localStorage`.
