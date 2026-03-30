const MAP_CACHE_PREFIX = 'focus.pwa.maps.cache.';
const MAP_QUEUE_PREFIX = 'focus.pwa.maps.queue.';

function hasLocalStorage() {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
}

function resolveScopedKey(prefix, scope) {
  return `${prefix}${encodeURIComponent(scope || 'default')}`;
}

function safeParse(rawValue, fallbackValue) {
  if (!rawValue) {
    return fallbackValue;
  }

  try {
    return JSON.parse(rawValue);
  } catch {
    return fallbackValue;
  }
}

export function loadCachedMapSnapshots(scope) {
  if (!hasLocalStorage()) {
    return [];
  }

  const parsed = safeParse(
    window.localStorage.getItem(resolveScopedKey(MAP_CACHE_PREFIX, scope)),
    [],
  );

  return Array.isArray(parsed)
    ? parsed.filter(isSerializableSnapshot)
    : [];
}

export function saveCachedMapSnapshots(scope, snapshots) {
  if (!hasLocalStorage()) {
    return;
  }

  const serializable = Array.isArray(snapshots)
    ? snapshots.filter(isSerializableSnapshot)
    : [];

  window.localStorage.setItem(
    resolveScopedKey(MAP_CACHE_PREFIX, scope),
    JSON.stringify(serializable),
  );
}

export function loadPendingMapOperations(scope) {
  if (!hasLocalStorage()) {
    return [];
  }

  const parsed = safeParse(
    window.localStorage.getItem(resolveScopedKey(MAP_QUEUE_PREFIX, scope)),
    [],
  );

  return Array.isArray(parsed) ? parsed : [];
}

export function savePendingMapOperations(scope, operations) {
  if (!hasLocalStorage()) {
    return;
  }

  window.localStorage.setItem(
    resolveScopedKey(MAP_QUEUE_PREFIX, scope),
    JSON.stringify(Array.isArray(operations) ? operations : []),
  );
}

function isSerializableSnapshot(snapshot) {
  return Boolean(
    snapshot &&
    typeof snapshot.filePath === 'string' &&
    typeof snapshot.fileName === 'string' &&
    typeof snapshot.mapName === 'string' &&
    typeof snapshot.revision === 'string' &&
    typeof snapshot.loadedAt === 'number' &&
    snapshot.document &&
    typeof snapshot.document === 'object',
  );
}
