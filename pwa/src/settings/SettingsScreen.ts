import { clearToken, getToken } from "../auth/sessionManager";
import { getSyncMetadata } from "../gitProvider/syncMetadata";

export interface SettingsScreenProps {
  mountNode: HTMLElement;
  onSessionRevoked?: () => void;
  onHardReset?: () => void;
}

export const renderSettingsScreen = ({
  mountNode,
  onSessionRevoked,
}: SettingsScreenProps): void => {
  const hasToken = Boolean(getToken());
  const syncMetadata = getSyncMetadata();

  mountNode.innerHTML = `
    <section aria-label="Settings">
      <h2>Settings</h2>
      <button id="revoke-session-button" type="button" ${hasToken ? "" : "disabled"}>
        Revoke local session
      </button>
      <p>
        Clears the token saved in this browser. You will need to enter a new token to access private repositories again.
      </p>
      <section aria-label="Sync diagnostics">
        <h3>Sync diagnostics</h3>
        <dl>
          <dt>Last sync time</dt>
          <dd>${formatSyncTime(syncMetadata.lastSyncAt)}</dd>
          <dt>Last sync result</dt>
          <dd>${syncMetadata.lastSyncResult ?? "Never synced"}</dd>
          <dt>Last error summary</dt>
          <dd>${syncMetadata.lastErrorSummary ?? "None"}</dd>
        </dl>
      </section>
    </section>
  `;

  const revokeButton = mountNode.querySelector<HTMLButtonElement>("#revoke-session-button");
  if (!revokeButton) {
    return;
  }

  revokeButton.addEventListener("click", () => {
    clearToken();
    revokeButton.disabled = true;
    onSessionRevoked?.();
  });
};

const formatSyncTime = (value: string | null): string => {
  if (!value) {
    return "Never synced";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString();
};
