import { TOKEN_STORAGE_KEY } from '../auth/sessionManager.js';

export const REPO_SETTINGS_STORAGE_KEY = 'focus.pwa.repo.settings';

const DEFAULT_BRANCH = 'main';
const DEFAULT_REPO_PATH = '/';

function hasLocalStorage() {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
}

function resolveSettingsStorageKey(storageKey) {
  return typeof storageKey === 'string' && storageKey.trim()
    ? storageKey.trim()
    : REPO_SETTINGS_STORAGE_KEY;
}

export function normalizeRepoPath(repoPath) {
  const value = typeof repoPath === 'string' ? repoPath.trim() : '';
  if (!value || value === '/') {
    return DEFAULT_REPO_PATH;
  }

  return value.replace(/^\/+|\/+$/g, '');
}

export function normalizeRepoSettings(rawSettings = {}) {
  return {
    repoOwner: typeof rawSettings.repoOwner === 'string' ? rawSettings.repoOwner.trim() : '',
    repoName: typeof rawSettings.repoName === 'string' ? rawSettings.repoName.trim() : '',
    repoBranch: typeof rawSettings.repoBranch === 'string' && rawSettings.repoBranch.trim()
      ? rawSettings.repoBranch.trim()
      : DEFAULT_BRANCH,
    repoPath: normalizeRepoPath(rawSettings.repoPath),
    tokenStorageKey:
      typeof rawSettings.tokenStorageKey === 'string' && rawSettings.tokenStorageKey.trim()
        ? rawSettings.tokenStorageKey.trim()
        : TOKEN_STORAGE_KEY,
  };
}

export function loadRepoSettings(storageKey = REPO_SETTINGS_STORAGE_KEY) {
  if (!hasLocalStorage()) {
    return normalizeRepoSettings();
  }

  const rawValue = window.localStorage.getItem(resolveSettingsStorageKey(storageKey));
  if (!rawValue) {
    return normalizeRepoSettings();
  }

  try {
    return normalizeRepoSettings(JSON.parse(rawValue));
  } catch {
    return normalizeRepoSettings();
  }
}

export function saveRepoSettings(settings, storageKey = REPO_SETTINGS_STORAGE_KEY) {
  if (!hasLocalStorage()) {
    return;
  }

  window.localStorage.setItem(
    resolveSettingsStorageKey(storageKey),
    JSON.stringify(normalizeRepoSettings(settings)),
  );
}

export function getEffectiveRepoSettings(runtimeConfig = {}) {
  const runtimeSettings = normalizeRepoSettings({
    repoOwner: runtimeConfig.repoOwner,
    repoName: runtimeConfig.repoName,
    repoBranch: runtimeConfig.repoBranch,
    repoPath: runtimeConfig.repoPath,
    tokenStorageKey: runtimeConfig.auth?.tokenStorageKey,
  });
  const persistedSettings = loadRepoSettings(runtimeConfig.auth?.settingsStorageKey);

  return normalizeRepoSettings({
    ...runtimeSettings,
    ...persistedSettings,
    tokenStorageKey:
      persistedSettings.tokenStorageKey ||
      runtimeSettings.tokenStorageKey ||
      TOKEN_STORAGE_KEY,
  });
}

export function isRepoSettingsComplete(settings) {
  return Boolean(
    settings?.repoOwner &&
    settings?.repoName &&
    settings?.repoBranch &&
    typeof settings?.repoPath === 'string',
  );
}

export function buildRepoFilePath(settings, fileName = 'todos.json') {
  const basePath = normalizeRepoPath(settings?.repoPath);
  return basePath === DEFAULT_REPO_PATH ? fileName : `${basePath}/${fileName}`;
}

export function describeRepoSettings(settings) {
  const normalized = normalizeRepoSettings(settings);
  const pathLabel = normalized.repoPath === DEFAULT_REPO_PATH ? '/' : `/${normalized.repoPath}`;
  return `${normalized.repoOwner}/${normalized.repoName}@${normalized.repoBranch}${pathLabel}`;
}

export function buildRepoScope(settings) {
  const normalized = normalizeRepoSettings(settings);
  return [
    normalized.repoOwner || 'owner',
    normalized.repoName || 'repo',
    normalized.repoBranch,
    normalized.repoPath,
  ].join('::').toLowerCase();
}
