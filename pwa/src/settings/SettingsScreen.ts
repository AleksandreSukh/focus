import { clearToken, getToken } from "../auth/sessionManager";

export interface SettingsScreenProps {
  mountNode: HTMLElement;
  onSessionRevoked?: () => void;
}

export const renderSettingsScreen = ({
  mountNode,
  onSessionRevoked,
}: SettingsScreenProps): void => {
  const hasToken = Boolean(getToken());

  mountNode.innerHTML = `
    <section aria-label="Settings">
      <h2>Settings</h2>
      <button id="revoke-session-button" type="button" ${hasToken ? "" : "disabled"}>
        Revoke local session
      </button>
      <p>
        Clears the token saved in this browser. You will need to enter a new token to access private repositories again.
      </p>
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
