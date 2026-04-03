function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function formatSyncTime(value) {
  if (!value) {
    return 'Never synced';
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString();
}

export function renderSettingsScreen({
  mountNode,
  repoSettings,
  fabSide,
  hasToken,
  syncMetadata,
  onSaveSettings,
  onClearToken,
  onRevalidate,
  onClose,
}) {
  mountNode.innerHTML = `
    <div class="modal-layer settings-modal-layer">
      <button type="button" id="settings-backdrop" class="modal-backdrop" aria-label="Close connection settings"></button>
      <section class="modal-card settings-card settings-modal-card" role="dialog" aria-modal="true" aria-labelledby="settings-title">
        <div class="settings-header">
          <div>
            <p class="eyebrow">Connection</p>
            <h2 id="settings-title">Connection settings</h2>
            <p class="card-copy">
              Update repository settings, validate access, or clear the saved PAT without wiping local cached map data.
            </p>
          </div>
          <button id="close-settings" type="button" class="ghost-button compact-button" data-modal-autofocus="true">Close</button>
        </div>
        <form id="settings-form" class="stack-form compact-form">
          <label>
            <span>Repository owner</span>
            <input name="repoOwner" type="text" required value="${escapeHtml(repoSettings.repoOwner)}" />
          </label>
          <label>
            <span>Repository name</span>
            <input name="repoName" type="text" required value="${escapeHtml(repoSettings.repoName)}" />
          </label>
          <label>
            <span>Branch</span>
            <input name="repoBranch" type="text" required value="${escapeHtml(repoSettings.repoBranch)}" />
          </label>
          <label>
            <span>FocusMaps folder path inside repository</span>
            <input name="repoPath" type="text" value="${escapeHtml(repoSettings.repoPath)}" />
          </label>
          <label>
            <span>Add item button location</span>
            <select name="fabSide">
              <option value="right" ${fabSide !== 'left' ? 'selected' : ''}>Right</option>
              <option value="left" ${fabSide === 'left' ? 'selected' : ''}>Left</option>
            </select>
          </label>
          <div class="form-actions">
            <button type="submit">Save settings</button>
            <button id="revalidate-connection" type="button" class="secondary-button" ${hasToken ? '' : 'disabled'}>
              Revalidate access
            </button>
            <button id="clear-token" type="button" class="danger-button" ${hasToken ? '' : 'disabled'}>
              Clear saved token
            </button>
          </div>
        </form>
        <section aria-label="Sync diagnostics" class="diagnostics-grid">
          <div>
            <h3>Sync diagnostics</h3>
            <dl>
              <dt>Last sync time</dt>
              <dd>${escapeHtml(formatSyncTime(syncMetadata.lastSyncAt))}</dd>
              <dt>Last sync state</dt>
              <dd>${escapeHtml(syncMetadata.lastSyncState ?? 'Never synced')}</dd>
              <dt>Last message</dt>
              <dd>${escapeHtml(syncMetadata.lastMessage ?? 'None')}</dd>
              <dt>Last error</dt>
              <dd>${escapeHtml(syncMetadata.lastErrorSummary ?? 'None')}</dd>
            </dl>
          </div>
          <div class="security-panel">
            <h3>Authentication model</h3>
            <p>
              This app uses a GitHub personal access token stored locally in this browser. Set the path to the
              FocusMaps directory itself, not to a single file. It does not use GitHub OAuth.
            </p>
          </div>
        </section>
      </section>
    </div>
  `;

  const settingsForm = mountNode.querySelector('#settings-form');
  const clearTokenButton = mountNode.querySelector('#clear-token');
  const revalidateButton = mountNode.querySelector('#revalidate-connection');
  const closeButton = mountNode.querySelector('#close-settings');
  const backdropButton = mountNode.querySelector('#settings-backdrop');

  if (closeButton) {
    closeButton.addEventListener('click', () => {
      onClose?.();
    });
  }

  if (backdropButton) {
    backdropButton.addEventListener('click', () => {
      onClose?.();
    });
  }

  if (settingsForm) {
    settingsForm.addEventListener('submit', async (event) => {
      event.preventDefault();
      const formData = new FormData(settingsForm);
      await onSaveSettings?.({
        repoOwner: String(formData.get('repoOwner') ?? '').trim(),
        repoName: String(formData.get('repoName') ?? '').trim(),
        repoBranch: String(formData.get('repoBranch') ?? '').trim(),
        repoPath: String(formData.get('repoPath') ?? '').trim() || '/',
        fabSide: String(formData.get('fabSide') ?? ''),
      });
    });
  }

  if (clearTokenButton) {
    clearTokenButton.addEventListener('click', () => {
      onClearToken?.();
    });
  }

  if (revalidateButton) {
    revalidateButton.addEventListener('click', () => {
      onRevalidate?.();
    });
  }
}
