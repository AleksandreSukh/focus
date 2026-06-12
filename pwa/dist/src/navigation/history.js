const TRANSIENT_OVERLAY_KINDS = new Set([
  'addChildNote',
  'addChildTask',
  'askAi',
  'audioViewer',
  'createMap',
  'deleteAttachment',
  'deleteMap',
  'deleteNode',
  'editNode',
  'imageViewer',
  'repairMap',
  'resolveConflict',
  'settings',
  'status',
  'textViewer',
]);

function stableCopy(value) {
  if (Array.isArray(value)) {
    return value.map(stableCopy);
  }

  if (!value || typeof value !== 'object') {
    return value;
  }

  return Object.keys(value)
    .sort()
    .reduce((copy, key) => {
      const nextValue = value[key];
      if (nextValue !== undefined) {
        copy[key] = stableCopy(nextValue);
      }
      return copy;
    }, {});
}

function normalizeOverlay(overlay) {
  if (!overlay || typeof overlay !== 'object' || Array.isArray(overlay)) {
    return null;
  }

  const kind = typeof overlay.kind === 'string' ? overlay.kind.trim() : '';
  if (!kind || TRANSIENT_OVERLAY_KINDS.has(kind)) {
    return null;
  }

  return stableCopy({
    ...overlay,
    kind,
  });
}

export function normalizeNavigationEntry(entry = {}) {
  const view = entry.view === 'tasks' || entry.view === 'map' ? entry.view : 'maps';
  const normalized = {
    view,
    mapPath: '',
    nodeId: '',
    overlay: normalizeOverlay(entry.overlay),
  };

  if (view === 'map') {
    normalized.mapPath = typeof entry.mapPath === 'string' ? entry.mapPath.trim() : '';
    normalized.nodeId = typeof entry.nodeId === 'string' ? entry.nodeId.trim() : '';
    if (!normalized.mapPath) {
      normalized.view = 'maps';
      normalized.nodeId = '';
    }
  }

  return normalized;
}

export function navigationEntryKey(entry) {
  return JSON.stringify(stableCopy(normalizeNavigationEntry(entry)));
}

export function navigationEntriesEqual(left, right) {
  return navigationEntryKey(left) === navigationEntryKey(right);
}

function normalizeStack(stack) {
  return Array.isArray(stack) ? stack.map(normalizeNavigationEntry) : [];
}

function compactBackStack(stack, current) {
  const compacted = [];
  stack.forEach((entry) => {
    if (compacted.length > 0 && navigationEntriesEqual(compacted[compacted.length - 1], entry)) {
      return;
    }

    compacted.push(entry);
  });

  while (compacted.length > 0 && navigationEntriesEqual(compacted[compacted.length - 1], current)) {
    compacted.pop();
  }

  return compacted;
}

function compactForwardStack(stack, current) {
  const compacted = [];
  stack.forEach((entry) => {
    if (compacted.length > 0 && navigationEntriesEqual(compacted[compacted.length - 1], entry)) {
      return;
    }

    compacted.push(entry);
  });

  while (compacted.length > 0 && navigationEntriesEqual(compacted[0], current)) {
    compacted.shift();
  }

  return compacted;
}

export function createNavigationHistory(initialEntry = { view: 'maps' }) {
  return {
    current: normalizeNavigationEntry(initialEntry),
    backStack: [],
    forwardStack: [],
  };
}

export function normalizeNavigationHistory(history, fallbackEntry = { view: 'maps' }) {
  if (!history || typeof history !== 'object' || Array.isArray(history)) {
    return createNavigationHistory(fallbackEntry);
  }

  const current = normalizeNavigationEntry(history.current || fallbackEntry);
  return {
    current,
    backStack: compactBackStack(normalizeStack(history.backStack), current),
    forwardStack: compactForwardStack(normalizeStack(history.forwardStack), current),
  };
}

export function canGoBack(history) {
  return normalizeNavigationHistory(history).backStack.length > 0;
}

export function canGoForward(history) {
  return normalizeNavigationHistory(history).forwardStack.length > 0;
}

export function pushNavigationEntry(history, entry) {
  const normalizedHistory = normalizeNavigationHistory(history);
  const nextEntry = normalizeNavigationEntry(entry);
  if (navigationEntriesEqual(normalizedHistory.current, nextEntry)) {
    return normalizedHistory;
  }

  return {
    current: nextEntry,
    backStack: [...normalizedHistory.backStack, normalizedHistory.current],
    forwardStack: [],
  };
}

export function replaceNavigationEntry(history, entry) {
  const normalizedHistory = normalizeNavigationHistory(history);
  const current = normalizeNavigationEntry(entry);
  return {
    current,
    backStack: compactBackStack(normalizedHistory.backStack, current),
    forwardStack: compactForwardStack(normalizedHistory.forwardStack, current),
  };
}

export function goBack(history) {
  const normalizedHistory = normalizeNavigationHistory(history);
  if (normalizedHistory.backStack.length === 0) {
    return normalizedHistory;
  }

  return {
    current: normalizedHistory.backStack[normalizedHistory.backStack.length - 1],
    backStack: normalizedHistory.backStack.slice(0, -1),
    forwardStack: [normalizedHistory.current, ...normalizedHistory.forwardStack],
  };
}

export function goForward(history) {
  const normalizedHistory = normalizeNavigationHistory(history);
  if (normalizedHistory.forwardStack.length === 0) {
    return normalizedHistory;
  }

  return {
    current: normalizedHistory.forwardStack[0],
    backStack: [...normalizedHistory.backStack, normalizedHistory.current],
    forwardStack: normalizedHistory.forwardStack.slice(1),
  };
}
