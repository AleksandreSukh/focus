export const SYNC_METADATA_STORAGE_KEY = 'focus.pwa.sync.metadata';

function hasLocalStorage() {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
}

function defaultSyncMetadata() {
  return {
    lastSyncAt: null,
    lastSyncState: null,
    lastMessage: null,
    lastErrorSummary: null,
  };
}

function persistSyncMetadata(metadata) {
  if (!hasLocalStorage()) {
    return;
  }

  window.localStorage.setItem(SYNC_METADATA_STORAGE_KEY, JSON.stringify(metadata));
}

export function getSyncMetadata() {
  if (!hasLocalStorage()) {
    return defaultSyncMetadata();
  }

  const rawValue = window.localStorage.getItem(SYNC_METADATA_STORAGE_KEY);
  if (!rawValue) {
    return defaultSyncMetadata();
  }

  try {
    const parsed = JSON.parse(rawValue);
    return {
      lastSyncAt: typeof parsed.lastSyncAt === 'string' ? parsed.lastSyncAt : null,
      lastSyncState: typeof parsed.lastSyncState === 'string' ? parsed.lastSyncState : null,
      lastMessage: typeof parsed.lastMessage === 'string' ? parsed.lastMessage : null,
      lastErrorSummary:
        typeof parsed.lastErrorSummary === 'string' ? parsed.lastErrorSummary : null,
    };
  } catch {
    return defaultSyncMetadata();
  }
}

export function recordSyncState(state, message = '') {
  const current = getSyncMetadata();
  persistSyncMetadata({
    ...current,
    lastSyncState: state,
    lastMessage: message || current.lastMessage,
  });
}

export function recordSyncSuccess(message = 'Sync succeeded.') {
  persistSyncMetadata({
    lastSyncAt: new Date().toISOString(),
    lastSyncState: 'success',
    lastMessage: message,
    lastErrorSummary: null,
  });
}

export function recordSyncFailure(summary, state = 'error') {
  persistSyncMetadata({
    lastSyncAt: new Date().toISOString(),
    lastSyncState: state,
    lastMessage: summary.trim().slice(0, 300),
    lastErrorSummary: summary.trim().slice(0, 300),
  });
}
