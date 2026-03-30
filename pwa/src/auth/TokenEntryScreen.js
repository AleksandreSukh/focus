export function renderTokenEntryScreen({
  mountNode,
  repoLabel,
  errorMessage = '',
}) {
  mountNode.innerHTML = `
    <section class="card connection-card" aria-label="GitHub access token">
      <h2>Enter GitHub personal access token</h2>
      <p class="card-copy">
        This PWA uses a GitHub personal access token for private static hosting.
        It does not use GitHub OAuth or a "Sign in with GitHub" flow.
      </p>
      <p class="connection-summary">${escapeHtml(repoLabel)}</p>
      <form id="token-entry-form" class="stack-form" novalidate>
        <label>
          <span>Personal access token</span>
          <input id="token-input" name="token" type="password" autocomplete="off" required />
        </label>
        <div class="form-actions">
          <button id="validate-token" type="submit">Validate and continue</button>
          <button id="edit-settings" type="button" class="secondary-button">Edit connection settings</button>
        </div>
      </form>
      <p id="token-error" class="form-error" role="alert" ${errorMessage ? '' : 'hidden'}>
        ${escapeHtml(errorMessage)}
      </p>
      <p class="security-note">
        Security note: the token is stored in this browser's local storage. Use a dedicated least-privilege token.
      </p>
    </section>
  `;
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
