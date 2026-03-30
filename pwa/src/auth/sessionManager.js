export const TOKEN_STORAGE_KEY = 'focus.pwa.auth.token';

function hasLocalStorage() {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
}

function resolveStorageKey(storageKey) {
  return typeof storageKey === 'string' && storageKey.trim()
    ? storageKey.trim()
    : TOKEN_STORAGE_KEY;
}

export function saveToken(token, storageKey = TOKEN_STORAGE_KEY) {
  if (!hasLocalStorage()) {
    return;
  }

  window.localStorage.setItem(resolveStorageKey(storageKey), String(token ?? '').trim());
}

export function getToken(storageKey = TOKEN_STORAGE_KEY) {
  if (!hasLocalStorage()) {
    return null;
  }

  const token = window.localStorage.getItem(resolveStorageKey(storageKey));
  if (!token) {
    return null;
  }

  const normalizedToken = token.trim();
  return normalizedToken.length > 0 ? normalizedToken : null;
}

export function clearToken(storageKey = TOKEN_STORAGE_KEY) {
  if (!hasLocalStorage()) {
    return;
  }

  window.localStorage.removeItem(resolveStorageKey(storageKey));
}
