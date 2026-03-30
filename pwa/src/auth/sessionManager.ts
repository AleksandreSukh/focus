const TOKEN_STORAGE_KEY = "focus.pwa.auth.token";

const hasLocalStorage = (): boolean =>
  typeof window !== "undefined" && typeof window.localStorage !== "undefined";

export const saveToken = (token: string): void => {
  if (!hasLocalStorage()) {
    return;
  }

  window.localStorage.setItem(TOKEN_STORAGE_KEY, token.trim());
};

export const getToken = (): string | null => {
  if (!hasLocalStorage()) {
    return null;
  }

  const token = window.localStorage.getItem(TOKEN_STORAGE_KEY);
  if (!token) {
    return null;
  }

  const normalizedToken = token.trim();
  return normalizedToken.length > 0 ? normalizedToken : null;
};

export const clearToken = (): void => {
  if (!hasLocalStorage()) {
    return;
  }

  window.localStorage.removeItem(TOKEN_STORAGE_KEY);
};

export { TOKEN_STORAGE_KEY };
