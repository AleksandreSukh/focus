const SYNC_METADATA_STORAGE_KEY = 'focus.pwa.sync.metadata';

type SyncResult = 'success' | 'error';

export type SyncMetadata = {
  lastSyncAt: string | null;
  lastSyncResult: SyncResult | null;
  lastErrorSummary: string | null;
};

const hasLocalStorage = (): boolean =>
  typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';

const defaultSyncMetadata = (): SyncMetadata => ({
  lastSyncAt: null,
  lastSyncResult: null,
  lastErrorSummary: null,
});

const persistSyncMetadata = (metadata: SyncMetadata): void => {
  if (!hasLocalStorage()) {
    return;
  }

  window.localStorage.setItem(SYNC_METADATA_STORAGE_KEY, JSON.stringify(metadata));
};

export const getSyncMetadata = (): SyncMetadata => {
  if (!hasLocalStorage()) {
    return defaultSyncMetadata();
  }

  const rawValue = window.localStorage.getItem(SYNC_METADATA_STORAGE_KEY);
  if (!rawValue) {
    return defaultSyncMetadata();
  }

  try {
    const parsed = JSON.parse(rawValue) as Partial<SyncMetadata>;
    const lastSyncResult =
      parsed.lastSyncResult === 'success' || parsed.lastSyncResult === 'error'
        ? parsed.lastSyncResult
        : null;

    return {
      lastSyncAt: typeof parsed.lastSyncAt === 'string' ? parsed.lastSyncAt : null,
      lastSyncResult,
      lastErrorSummary:
        typeof parsed.lastErrorSummary === 'string' ? parsed.lastErrorSummary : null,
    };
  } catch {
    return defaultSyncMetadata();
  }
};

export const recordSyncSuccess = (): void => {
  const existing = getSyncMetadata();

  persistSyncMetadata({
    lastSyncAt: new Date().toISOString(),
    lastSyncResult: 'success',
    lastErrorSummary: existing.lastErrorSummary,
  });
};

export const recordSyncFailure = (summary: string): void => {
  persistSyncMetadata({
    lastSyncAt: new Date().toISOString(),
    lastSyncResult: 'error',
    lastErrorSummary: summary.trim().slice(0, 300),
  });
};

export { SYNC_METADATA_STORAGE_KEY };
