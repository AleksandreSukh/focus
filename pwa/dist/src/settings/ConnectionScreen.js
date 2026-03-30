export function renderConnectionScreen({
  mountNode,
  initialValues,
  errorMessage = '',
}) {
  mountNode.innerHTML = `
    <section class="card connection-card" aria-label="GitHub connection settings">
      <h2>Connect this PWA to GitHub</h2>
      <p class="card-copy">
        This is a private static PWA. It uses repository settings plus a GitHub personal access token.
        It does not use GitHub OAuth.
      </p>
      <form id="connection-form" class="stack-form" novalidate>
        <label>
          <span>Repository owner</span>
          <input name="repoOwner" type="text" required value="${escapeHtml(initialValues.repoOwner)}" />
        </label>
        <label>
          <span>Repository name</span>
          <input name="repoName" type="text" required value="${escapeHtml(initialValues.repoName)}" />
        </label>
        <label>
          <span>Branch</span>
          <input name="repoBranch" type="text" required value="${escapeHtml(initialValues.repoBranch)}" />
        </label>
        <label>
          <span>Folder path inside repository</span>
          <input name="repoPath" type="text" value="${escapeHtml(initialValues.repoPath)}" placeholder="/" />
        </label>
        <label>
          <span>GitHub personal access token</span>
          <input name="token" type="password" autocomplete="off" required />
        </label>
        <div class="form-actions">
          <button id="save-connection" type="submit">Save connection and continue</button>
        </div>
      </form>
      <p id="connection-error" class="form-error" role="alert" ${errorMessage ? '' : 'hidden'}>
        ${escapeHtml(errorMessage)}
      </p>
      <p class="security-note">
        Recommended token scope: repository contents read/write for this repo only.
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
