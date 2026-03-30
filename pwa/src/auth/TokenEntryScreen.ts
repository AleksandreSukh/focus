import { saveToken } from "./sessionManager";
import { validateToken, type TokenProbeOptions } from "./tokenValidation";

export interface TokenEntryScreenProps {
  mountNode: HTMLElement;
  probe: TokenProbeOptions;
  onAuthenticated?: () => void;
}

export const renderTokenEntryScreen = ({
  mountNode,
  probe,
  onAuthenticated,
}: TokenEntryScreenProps): void => {
  mountNode.innerHTML = `
    <section aria-label="Authentication">
      <h1>Connect to private repository</h1>
      <p>Enter a personal access token to continue.</p>
      <form id="token-entry-form">
        <label for="token-input">Access token</label>
        <input id="token-input" name="token" type="password" autocomplete="off" required />
        <button type="submit">Save and continue</button>
      </form>
      <p id="auth-error" role="alert" hidden></p>
      <p>
        <strong>Security note:</strong>
        This token is stored in localStorage on this device and is not encrypted at rest.
        Use a dedicated, least-privilege token and revoke it if the device is shared.
      </p>
    </section>
  `;

  const form = mountNode.querySelector<HTMLFormElement>("#token-entry-form");
  const tokenInput = mountNode.querySelector<HTMLInputElement>("#token-input");
  const errorMessage = mountNode.querySelector<HTMLElement>("#auth-error");

  if (!form || !tokenInput || !errorMessage) {
    return;
  }

  form.addEventListener("submit", async (event) => {
    event.preventDefault();

    const token = tokenInput.value.trim();
    if (!token) {
      errorMessage.hidden = false;
      errorMessage.textContent = "Token is required.";
      return;
    }

    const validation = await validateToken(token, probe);
    if (!validation.ok) {
      errorMessage.hidden = false;
      errorMessage.textContent = validation.error.message;
      return;
    }

    saveToken(token);
    errorMessage.hidden = true;
    errorMessage.textContent = "";
    onAuthenticated?.();
  });
};
