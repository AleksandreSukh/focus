import {
  buildNodeAddCommitMessage,
  buildNodeEditCommitMessage,
  buildNodeTaskStateCommitMessage,
  getSyncMetadata,
  recordSyncState,
} from './gitProvider/index.js';
import { clearToken, getToken, saveToken, validateToken } from './auth/index.js';
import { renderTokenEntryScreen } from './auth/TokenEntryScreen.js';
import {
  buildRepoScope,
  describeRepoSettings,
  getEffectiveRepoSettings,
  isRepoSettingsComplete,
  normalizeRepoSettings,
  saveRepoSettings,
} from './settings/repoSettings.js';
import { renderConnectionScreen } from './settings/ConnectionScreen.js';
import { renderSettingsScreen } from './settings/SettingsScreen.js';
import { GitHubMindMapProvider } from './maps/githubMindMapProvider.js';
import {
  loadCachedMapSnapshots,
  loadPendingMapOperations,
  saveCachedMapSnapshots,
  savePendingMapOperations,
} from './maps/localCache.js';
import { MindMapRepository } from './maps/mindMapRepository.js';
import { MindMapService } from './maps/mindMapService.js';
import {
  applyMapMutation,
  buildMapSummary,
  cloneMapDocument,
  collectTaskEntries,
  getNodeBadges,
  displayTaskState,
  findNodeRecord,
  getNodeUiState,
  nowIso,
  TASK_STATE,
} from './maps/model.js';

const state = {
  runtimeConfig: null,
  repoSettings: null,
  repoScope: '',
  authState: 'missingConfig',
  connectionError: '',
  tokenError: '',
  syncState: {
    kind: 'idle',
    tone: 'warning',
    message: 'Connection required.',
    detail: '',
    canRetry: false,
  },
  service: null,
  installEvent: null,
  showSettings: false,
  processingQueue: false,
  currentView: 'maps',
  taskFilter: 'open',
  selectedMapPath: '',
  selectedNodeId: '',
  mapsByPath: {},
  pendingOperations: [],
  localCollapsedByMap: {},
};

const ui = {};
let globalUiListenersBound = false;

export async function bootstrapApp() {
  wireUi();
  registerPwaShell();

  state.runtimeConfig = resolveRuntimeConfig();
  switchRepoContext(getEffectiveRepoSettings(state.runtimeConfig));
  updateInstallState();

  if (!isRepoSettingsComplete(state.repoSettings)) {
    state.authState = 'missingConfig';
    state.syncState = {
      kind: 'idle',
      tone: 'warning',
      message: 'Repository settings are required before sync can start.',
      detail: '',
      canRetry: false,
    };
    render();
    return;
  }

  const token = getToken(state.repoSettings.tokenStorageKey);
  if (!token) {
    state.authState = 'needsToken';
    state.syncState = {
      kind: 'idle',
      tone: 'warning',
      message: 'A GitHub personal access token is required to sync.',
      detail: describeRepoSettings(state.repoSettings),
      canRetry: false,
    };
    render();
    return;
  }

  await authenticateAndLoad();
}

function wireUi() {
  ui.installButton = document.getElementById('install-button');
  ui.installFallback = document.getElementById('install-fallback');
  ui.settingsToggle = document.getElementById('settings-toggle');
  ui.statusMessage = document.getElementById('sync-status');
  ui.statusDetail = document.getElementById('sync-detail');
  ui.retryButton = document.getElementById('retry-sync');
  ui.screenRoot = document.getElementById('screen-root');
  ui.appRoot = document.getElementById('app-root');
  ui.settingsRoot = document.getElementById('settings-root');

  bindGlobalUiListeners();

  ui.settingsToggle?.addEventListener('click', () => {
    state.showSettings = !state.showSettings;
    render();
  });

  ui.retryButton?.addEventListener('click', () => {
    void handleRetryButtonClick();
  });

  ui.installButton?.addEventListener('click', async () => {
    if (!state.installEvent) {
      return;
    }

    state.installEvent.prompt();
    await state.installEvent.userChoice;
    state.installEvent = null;
    updateInstallState();
  });

  window.addEventListener('beforeinstallprompt', (event) => {
    event.preventDefault();
    state.installEvent = event;
    updateInstallState();
  });

  window.addEventListener('appinstalled', () => {
    state.installEvent = null;
    updateInstallState();
  });
}

function bindGlobalUiListeners() {
  if (globalUiListenersBound) {
    return;
  }

  document.addEventListener('submit', handleDocumentSubmit, true);
  document.addEventListener('click', handleDocumentClick, true);
  globalUiListenersBound = true;
}

async function handleDocumentSubmit(event) {
  const form = event.target;
  if (!(form instanceof HTMLFormElement)) {
    return;
  }

  switch (form.id) {
    case 'connection-form':
      event.preventDefault();
      await handleConnectionFormSubmit(form);
      return;
    case 'token-entry-form':
      event.preventDefault();
      await handleTokenFormSubmit(form);
      return;
    case 'edit-node-form':
      event.preventDefault();
      await handleEditNodeSubmit(form);
      return;
    case 'add-note-form':
      event.preventDefault();
      await handleAddChildSubmit(form, 'addChildNote');
      return;
    case 'add-task-form':
      event.preventDefault();
      await handleAddChildSubmit(form, 'addChildTask');
      return;
    default:
      return;
  }
}

function handleDocumentClick(event) {
  const target = event.target;
  if (!(target instanceof Element)) {
    return;
  }

  const button = target.closest('button');
  if (!(button instanceof HTMLButtonElement)) {
    return;
  }

  if (button.id === 'edit-settings') {
    event.preventDefault();
    state.authState = 'missingConfig';
    state.connectionError = '';
    state.tokenError = '';
    render();
    return;
  }

  if (button.id === 'validate-token' || button.id === 'save-connection') {
    const form = button.closest('form');
    if (!(form instanceof HTMLFormElement)) {
      return;
    }

    event.preventDefault();
    if (button.id === 'validate-token') {
      void handleTokenFormSubmit(form);
      return;
    }

    void handleConnectionFormSubmit(form);
    return;
  }

  const action = button.dataset.action;
  if (!action) {
    return;
  }

  event.preventDefault();
  switch (action) {
    case 'switch-view':
      state.currentView = button.dataset.view === 'tasks' ? 'tasks' : 'maps';
      render();
      return;
    case 'refresh-maps':
      void loadWorkspace(true);
      return;
    case 'open-map':
      openMap(button.dataset.mapPath || '');
      return;
    case 'back-to-maps':
      state.currentView = 'maps';
      render();
      return;
    case 'select-node':
      selectNode(button.dataset.mapPath || '', button.dataset.nodeId || '');
      return;
    case 'toggle-node':
      toggleLocalNodeCollapse(button.dataset.mapPath || '', button.dataset.nodeId || '');
      return;
    case 'set-task-state':
      void handleSetTaskState(button.dataset.taskState || '');
      return;
    case 'open-task-node':
      openTaskNode(button.dataset.mapPath || '', button.dataset.nodeId || '');
      return;
    case 'set-task-filter':
      state.taskFilter = button.dataset.filter || 'open';
      render();
      return;
    default:
      return;
  }
}

async function handleRetryButtonClick() {
  state.syncState = {
    kind: 'loadingRemote',
    tone: 'pending',
    message: 'Retrying sync...',
    detail: buildStatusDetail(),
    canRetry: false,
  };
  render();
  await handleRetry();
}

function resolveRuntimeConfig() {
  return window.__FOCUS_RUNTIME_CONFIG__ ?? {
    host: 'github-pages',
    repoOwner: '',
    repoName: '',
    repoBranch: 'main',
    repoPath: '/',
    auth: {
      tokenStorageKey: 'focus_runtime_token',
    },
  };
}

function registerPwaShell() {
  window.addEventListener('load', async () => {
    if (!('serviceWorker' in navigator)) {
      return;
    }

    if (isLocalDevelopmentHost()) {
      await disableLocalServiceWorkerCaching();
      return;
    }

    try {
      await navigator.serviceWorker.register('./sw.js');
    } catch (error) {
      console.error('Service worker registration failed', error);
    }
  });
}

function isLocalDevelopmentHost() {
  return ['localhost', '127.0.0.1', '[::1]'].includes(window.location.hostname);
}

async function disableLocalServiceWorkerCaching() {
  try {
    const registrations = await navigator.serviceWorker.getRegistrations();
    await Promise.all(registrations.map((registration) => registration.unregister()));

    if ('caches' in window) {
      const cacheKeys = await caches.keys();
      const focusCacheKeys = cacheKeys.filter((cacheKey) => cacheKey.startsWith('focus-pwa-shell-'));
      await Promise.all(focusCacheKeys.map((cacheKey) => caches.delete(cacheKey)));
    }
  } catch (error) {
    console.warn('Failed to clear local service worker caches', error);
  }
}

function updateInstallState() {
  if (ui.installButton) {
    ui.installButton.hidden = !state.installEvent;
  }

  if (ui.installFallback) {
    ui.installFallback.hidden = 'BeforeInstallPromptEvent' in window || Boolean(state.installEvent);
  }
}

function switchRepoContext(repoSettings) {
  state.repoSettings = normalizeRepoSettings(repoSettings);
  state.repoScope = buildRepoScope(state.repoSettings);
  setSnapshots(loadCachedMapSnapshots(state.repoScope));
  state.pendingOperations = loadPendingMapOperations(state.repoScope);
  state.service = null;
  state.currentView = 'maps';
  state.selectedMapPath = '';
  state.selectedNodeId = '';
  state.showSettings = false;
  state.localCollapsedByMap = {};
}

async function authenticateAndLoad() {
  if (!isRepoSettingsComplete(state.repoSettings)) {
    state.authState = 'missingConfig';
    state.connectionError = 'Repository owner, repository name, branch, and FocusMaps path are required.';
    render();
    return;
  }

  const token = getToken(state.repoSettings.tokenStorageKey);
  if (!token) {
    state.authState = 'needsToken';
    state.tokenError = '';
    render();
    return;
  }

  state.authState = 'validating';
  state.connectionError = '';
  state.tokenError = '';
  state.syncState = {
    kind: 'loadingRemote',
    tone: 'pending',
    message: 'Validating GitHub access...',
    detail: describeRepoSettings(state.repoSettings),
    canRetry: false,
  };
  render();

  const validation = await validateToken(token, state.repoSettings);
  if (!validation.ok) {
    state.service = null;
    state.syncState = {
      kind: 'error',
      tone: validation.error.code === 'RATE_LIMIT' ? 'warning' : 'error',
      message: validation.error.message,
      detail: buildErrorDetail(validation.error),
      canRetry: validation.error.code === 'RATE_LIMIT' || validation.error.code === 'NETWORK',
    };

    if (validation.error.code === 'NOT_FOUND') {
      state.authState = 'missingConfig';
      state.connectionError = validation.error.message;
    } else {
      if (validation.error.code === 'UNAUTHORIZED' || validation.error.code === 'FORBIDDEN') {
        clearToken(state.repoSettings.tokenStorageKey);
      }
      state.authState = 'authFailed';
      state.tokenError = validation.error.message;
    }

    render();
    return;
  }

  state.authState = 'authenticated';
  state.service = createMindMapService(state.repoSettings, token);
  await loadWorkspace(true);
}

function createMindMapService(repoSettings, token) {
  const provider = new GitHubMindMapProvider({
    owner: repoSettings.repoOwner,
    repo: repoSettings.repoName,
    branch: repoSettings.repoBranch,
    token,
    directoryPath: repoSettings.repoPath,
  });

  return new MindMapService(new MindMapRepository(provider));
}

async function loadWorkspace(forceRefresh) {
  if (!state.service) {
    return;
  }

  recordSyncState('loadingRemote', 'Loading maps from GitHub.');
  state.syncState = {
    kind: 'loadingRemote',
    tone: 'pending',
    message: 'Loading mind maps from GitHub...',
    detail: describeRepoSettings(state.repoSettings),
    canRetry: false,
  };
  render();

  const loaded = await state.service.listMaps(forceRefresh);
  if (!loaded.ok) {
    handleSyncFailure(loaded.error, 'Could not load mind maps from GitHub.');
    render();
    return;
  }

  const hydratedSnapshots = applyPendingOperationsLocally(loaded.value, state.pendingOperations);
  state.service.hydrateSnapshots(loaded.value);
  setSnapshots(hydratedSnapshots);
  persistRepoScopedState();

  state.authState = 'authenticated';
  state.syncState = {
    kind: 'success',
    tone: 'success',
    message: `Connected to ${state.repoSettings.repoOwner}/${state.repoSettings.repoName}.`,
    detail: buildStatusDetail(),
    canRetry: false,
  };
  render();

  if (state.pendingOperations.length > 0) {
    void processPendingOperations();
  }
}

function enqueueOperation(operation) {
  const currentSnapshot = state.mapsByPath[operation.filePath];
  if (!currentSnapshot) {
    return;
  }

  const nextSnapshot = {
    ...currentSnapshot,
    document: cloneMapDocument(currentSnapshot.document),
    loadedAt: Date.now(),
  };
  const applied = applyMapMutation(nextSnapshot.document, operation);
  if (!applied.ok) {
    state.syncState = {
      kind: 'error',
      tone: 'error',
      message: applied.error.message,
      detail: buildErrorDetail(applied.error),
      canRetry: false,
    };
    render();
    return;
  }

  state.pendingOperations = [...state.pendingOperations, operation];
  replaceSnapshot(nextSnapshot);
  state.currentView = 'map';
  state.selectedMapPath = operation.filePath;
  state.selectedNodeId = applied.value.selectedNodeId || state.selectedNodeId;
  persistRepoScopedState();
  state.syncState = {
    kind: 'syncing',
    tone: 'pending',
    message: `${state.pendingOperations.length} local change${state.pendingOperations.length === 1 ? '' : 's'} waiting to sync.`,
    detail: buildStatusDetail(),
    canRetry: false,
  };
  render();
  void processPendingOperations();
}

async function processPendingOperations() {
  if (state.processingQueue || state.authState !== 'authenticated' || !state.service) {
    return;
  }

  if (state.pendingOperations.length === 0) {
    state.syncState = {
      kind: 'success',
      tone: 'success',
      message: 'All local changes are synced.',
      detail: buildStatusDetail(),
      canRetry: false,
    };
    render();
    return;
  }

  state.processingQueue = true;
  render();

  while (state.pendingOperations.length > 0) {
    const currentOperation = state.pendingOperations[0];
    recordSyncState('syncing', currentOperation.commitMessage);
    state.syncState = {
      kind: 'syncing',
      tone: 'pending',
      message: `Syncing ${describeOperation(currentOperation)}...`,
      detail: buildStatusDetail(),
      canRetry: false,
    };
    render();

    const result = await state.service.applyMutation(
      currentOperation.filePath,
      currentOperation,
      currentOperation.commitMessage,
    );

    if (!result.ok) {
      state.processingQueue = false;
      handleSyncFailure(result.error, `Could not sync ${describeOperation(currentOperation)}.`);
      render();
      return;
    }

    replaceSnapshot(result.value.snapshot);
    state.pendingOperations = state.pendingOperations.slice(1);
    setSnapshots(applyPendingOperationsLocally(getSnapshots(), state.pendingOperations));
    persistRepoScopedState();
    state.syncState = {
      kind: 'success',
      tone: 'success',
      message: `Synced ${describeOperation(currentOperation)}.`,
      detail: buildStatusDetail(),
      canRetry: false,
    };
    render();
  }

  state.processingQueue = false;
  state.syncState = {
    kind: 'success',
    tone: 'success',
    message: 'All local changes are synced.',
    detail: buildStatusDetail(),
    canRetry: false,
  };
  persistRepoScopedState();
  render();
}

function handleSyncFailure(error, fallbackMessage) {
  const gitHubCause = error?.cause;
  const errorCode = gitHubCause?.code || error?.code;
  const message = error?.message || fallbackMessage;
  const isRetriable =
    Boolean(error?.retriable) ||
    errorCode === 'NETWORK' ||
    errorCode === 'RATE_LIMIT' ||
    errorCode === 'STALE_STATE';

  if (errorCode === 'UNAUTHORIZED' || errorCode === 'FORBIDDEN') {
    clearToken(state.repoSettings.tokenStorageKey);
    state.authState = 'authFailed';
    state.tokenError = message;
    state.service = null;
  } else if (errorCode === 'NOT_FOUND') {
    state.authState = 'missingConfig';
    state.connectionError = message;
    state.service = null;
  }

  state.syncState = {
    kind: errorCode === 'STALE_STATE' || error?.code === 'CONFLICT_UNRESOLVED' ? 'conflict' : 'error',
    tone: errorCode === 'RATE_LIMIT' ? 'warning' : 'error',
    message,
    detail: buildErrorDetail(error),
    canRetry: isRetriable || state.pendingOperations.length > 0,
  };
}

async function handleRetry() {
  if (!isRepoSettingsComplete(state.repoSettings)) {
    state.authState = 'missingConfig';
    state.connectionError = 'Repository owner, repository name, branch, and FocusMaps path are required.';
    render();
    return;
  }

  const token = getToken(state.repoSettings.tokenStorageKey);
  if (!token) {
    state.authState = 'needsToken';
    state.tokenError = 'Enter a GitHub personal access token to continue.';
    render();
    return;
  }

  if (!state.service) {
    await authenticateAndLoad();
    return;
  }

  if (state.pendingOperations.length > 0) {
    await processPendingOperations();
    return;
  }

  await loadWorkspace(true);
}

async function handleConnectionFormSubmit(form) {
  const formData = new FormData(form);
  const nextSettings = normalizeRepoSettings({
    repoOwner: String(formData.get('repoOwner') ?? '').trim(),
    repoName: String(formData.get('repoName') ?? '').trim(),
    repoBranch: String(formData.get('repoBranch') ?? '').trim(),
    repoPath: String(formData.get('repoPath') ?? '').trim(),
    tokenStorageKey: state.runtimeConfig?.auth?.tokenStorageKey,
  });
  const token = String(formData.get('token') ?? '').trim();

  state.connectionError = '';
  state.tokenError = '';

  if (!isRepoSettingsComplete(nextSettings)) {
    state.authState = 'missingConfig';
    state.connectionError = 'Repository owner, repository name, branch, and FocusMaps path are required.';
    render();
    return;
  }

  if (!token) {
    state.authState = 'missingConfig';
    state.connectionError = 'A GitHub personal access token is required to validate this connection.';
    render();
    return;
  }

  saveRepoSettings(nextSettings, state.runtimeConfig?.auth?.settingsStorageKey);
  saveToken(token, nextSettings.tokenStorageKey);
  switchRepoContext(nextSettings);
  await authenticateAndLoad();
}

async function handleTokenFormSubmit(form) {
  const formData = new FormData(form);
  const token = String(formData.get('token') ?? '').trim();

  if (!token) {
    state.authState = 'needsToken';
    state.tokenError = 'Enter a GitHub personal access token to continue.';
    render();
    return;
  }

  saveToken(token, state.repoSettings.tokenStorageKey);
  await authenticateAndLoad();
}

async function handleEditNodeSubmit(form) {
  const snapshot = getSelectedSnapshot();
  const selectedNode = getSelectedNodeUiState(snapshot);
  if (!snapshot || !selectedNode) {
    return;
  }

  const text = String(new FormData(form).get('text') ?? '').trim();
  const operation = {
    type: 'editNodeText',
    filePath: snapshot.filePath,
    nodeId: selectedNode.node.uniqueIdentifier,
    text,
    timestamp: nowIso(),
    commitMessage: buildNodeEditCommitMessage(snapshot.mapName, selectedNode.node.uniqueIdentifier),
  };

  enqueueOperation(operation);
}

async function handleAddChildSubmit(form, type) {
  const snapshot = getSelectedSnapshot();
  const selectedNode = getSelectedNodeUiState(snapshot);
  if (!snapshot || !selectedNode) {
    return;
  }

  const text = String(new FormData(form).get('text') ?? '').trim();
  const operation = {
    type,
    filePath: snapshot.filePath,
    parentNodeId: selectedNode.node.uniqueIdentifier,
    newNodeId: createClientNodeId(),
    text,
    timestamp: nowIso(),
    commitMessage: buildNodeAddCommitMessage(
      snapshot.mapName,
      text,
      type === 'addChildTask' ? 'task' : 'note',
    ),
  };

  enqueueOperation(operation);
}

async function handleSetTaskState(taskStateValue) {
  const snapshot = getSelectedSnapshot();
  const selectedNode = getSelectedNodeUiState(snapshot);
  if (!snapshot || !selectedNode) {
    return;
  }

  const nextTaskState = Number.parseInt(String(taskStateValue ?? ''), 10);
  if (!Number.isInteger(nextTaskState)) {
    return;
  }

  enqueueOperation({
    type: 'setTaskState',
    filePath: snapshot.filePath,
    nodeId: selectedNode.node.uniqueIdentifier,
    taskState: nextTaskState,
    timestamp: nowIso(),
    commitMessage: buildNodeTaskStateCommitMessage(
      snapshot.mapName,
      selectedNode.node.uniqueIdentifier,
      nextTaskState,
    ),
  });
}

function openMap(mapPath) {
  const snapshot = state.mapsByPath[mapPath];
  if (!snapshot) {
    return;
  }

  state.currentView = 'map';
  state.selectedMapPath = mapPath;
  if (!findNodeRecord(snapshot.document, state.selectedNodeId)) {
    state.selectedNodeId = snapshot.document.rootNode?.uniqueIdentifier || '';
  }
  render();
}

function openTaskNode(mapPath, nodeId) {
  const snapshot = state.mapsByPath[mapPath];
  if (!snapshot) {
    return;
  }

  state.currentView = 'map';
  state.selectedMapPath = mapPath;
  state.selectedNodeId = findNodeRecord(snapshot.document, nodeId)
    ? nodeId
    : snapshot.document.rootNode?.uniqueIdentifier || '';
  render();
}

function selectNode(mapPath, nodeId) {
  const snapshot = state.mapsByPath[mapPath];
  if (!snapshot) {
    return;
  }

  state.currentView = 'map';
  state.selectedMapPath = mapPath;
  state.selectedNodeId = findNodeRecord(snapshot.document, nodeId)
    ? nodeId
    : snapshot.document.rootNode?.uniqueIdentifier || '';
  render();
}

function toggleLocalNodeCollapse(mapPath, nodeId) {
  const mapCollapseState = state.localCollapsedByMap[mapPath] ?? {};
  const snapshot = state.mapsByPath[mapPath];
  if (!snapshot) {
    return;
  }

  const record = findNodeRecord(snapshot.document, nodeId);
  if (!record || !Array.isArray(record.node.children) || record.node.children.length === 0) {
    return;
  }

  const isCollapsed = getLocalCollapsedState(mapPath, record.node);
  state.localCollapsedByMap = {
    ...state.localCollapsedByMap,
    [mapPath]: {
      ...mapCollapseState,
      [nodeId]: !isCollapsed,
    },
  };
  render();
}

function setSnapshots(snapshots) {
  const nextByPath = {};
  snapshots.forEach((snapshot) => {
    if (snapshot?.filePath) {
      nextByPath[snapshot.filePath] = snapshot;
    }
  });
  state.mapsByPath = nextByPath;

  if (!state.selectedMapPath || !nextByPath[state.selectedMapPath]) {
    state.selectedMapPath = '';
    state.selectedNodeId = '';
    if (state.currentView === 'map') {
      state.currentView = 'maps';
    }
    return;
  }

  const selectedSnapshot = nextByPath[state.selectedMapPath];
  if (!findNodeRecord(selectedSnapshot.document, state.selectedNodeId)) {
    state.selectedNodeId = selectedSnapshot.document.rootNode?.uniqueIdentifier || '';
  }
}

function replaceSnapshot(snapshot) {
  if (!snapshot?.filePath) {
    return;
  }

  setSnapshots([
    ...getSnapshots().filter((item) => item.filePath !== snapshot.filePath),
    snapshot,
  ]);
}

function getSnapshots() {
  return Object.values(state.mapsByPath)
    .sort((left, right) => left.fileName.localeCompare(right.fileName));
}

function getSelectedSnapshot() {
  if (state.selectedMapPath && state.mapsByPath[state.selectedMapPath]) {
    return state.mapsByPath[state.selectedMapPath];
  }

  return null;
}

function getSelectedNodeUiState(snapshot = getSelectedSnapshot()) {
  if (!snapshot) {
    return null;
  }

  const selectedRecord = getNodeUiState(
    snapshot.document,
    state.selectedNodeId || snapshot.document.rootNode?.uniqueIdentifier,
  );

  if (selectedRecord) {
    if (!state.selectedNodeId) {
      state.selectedNodeId = selectedRecord.node.uniqueIdentifier;
    }
    return selectedRecord;
  }

  return getNodeUiState(snapshot.document, snapshot.document.rootNode?.uniqueIdentifier || '');
}

function applyPendingOperationsLocally(snapshots, pendingOperations) {
  const snapshotsByPath = new Map(
    snapshots.map((snapshot) => [
      snapshot.filePath,
      {
        ...snapshot,
        document: cloneMapDocument(snapshot.document),
      },
    ]),
  );

  pendingOperations.forEach((operation) => {
    const snapshot = snapshotsByPath.get(operation.filePath);
    if (!snapshot) {
      return;
    }

    const applied = applyMapMutation(snapshot.document, operation);
    if (applied.ok) {
      snapshot.loadedAt = Date.now();
    }
  });

  return Array.from(snapshotsByPath.values());
}

function persistRepoScopedState() {
  saveCachedMapSnapshots(state.repoScope, getSnapshots());
  savePendingMapOperations(state.repoScope, state.pendingOperations);
}

function getLocalCollapsedState(mapPath, node) {
  const mapCollapseState = state.localCollapsedByMap[mapPath];
  if (mapCollapseState && Object.prototype.hasOwnProperty.call(mapCollapseState, node.uniqueIdentifier)) {
    return Boolean(mapCollapseState[node.uniqueIdentifier]);
  }

  return Boolean(node.collapsed);
}

function createClientNodeId() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID();
  }

  return `${Date.now().toString(16).padStart(8, '0')}-0000-4000-8000-${Math.random().toString(16).slice(2, 14).padEnd(12, '0')}`;
}

function render() {
  renderStatusPanel();

  if (ui.settingsToggle) {
    ui.settingsToggle.hidden = state.authState !== 'authenticated';
  }

  if (state.authState === 'authenticated') {
    renderWorkspace();
    renderSettingsPanel();
    return;
  }

  if (ui.screenRoot) {
    ui.screenRoot.hidden = false;
  }
  if (ui.appRoot) {
    ui.appRoot.hidden = true;
    ui.appRoot.innerHTML = '';
  }
  if (ui.settingsRoot) {
    ui.settingsRoot.hidden = true;
    ui.settingsRoot.innerHTML = '';
  }

  renderGateScreen();
}

function renderStatusPanel() {
  if (ui.statusMessage) {
    ui.statusMessage.textContent = state.syncState.message;
    ui.statusMessage.dataset.tone = state.syncState.tone;
  }

  if (ui.statusDetail) {
    ui.statusDetail.textContent = state.syncState.detail || '';
  }

  if (ui.retryButton) {
    ui.retryButton.hidden = !state.syncState.canRetry;
  }
}

function renderGateScreen() {
  if (!ui.screenRoot) {
    return;
  }

  if (state.authState === 'missingConfig') {
    renderConnectionScreen({
      mountNode: ui.screenRoot,
      initialValues: state.repoSettings,
      errorMessage: state.connectionError,
    });
    return;
  }

  if (state.authState === 'needsToken' || state.authState === 'authFailed') {
    renderTokenEntryScreen({
      mountNode: ui.screenRoot,
      repoLabel: describeRepoSettings(state.repoSettings),
      errorMessage: state.tokenError,
    });
    return;
  }

  ui.screenRoot.innerHTML = `
    <section class="card connection-card" aria-label="Connecting to GitHub">
      <h2>Connecting to GitHub</h2>
      <p class="card-copy">${escapeHtml(state.syncState.message)}</p>
      <p class="security-note">${escapeHtml(state.syncState.detail || describeRepoSettings(state.repoSettings))}</p>
    </section>
  `;
}

function renderWorkspace() {
  if (!ui.appRoot) {
    return;
  }

  if (ui.screenRoot) {
    ui.screenRoot.hidden = true;
    ui.screenRoot.innerHTML = '';
  }

  ui.appRoot.hidden = false;

  const summaries = getSnapshots().map((snapshot) => buildMapSummary(snapshot));
  const viewMarkup =
    state.currentView === 'tasks'
      ? renderTasksView()
      : state.currentView === 'map'
        ? renderMapView()
        : renderMapsView(summaries);

  ui.appRoot.innerHTML = `
    <section class="workspace-shell" aria-label="Mind map workspace">
      <nav class="workspace-nav" aria-label="Workspace navigation">
        <div class="nav-tabs">
          <button
            type="button"
            class="nav-tab ${state.currentView === 'tasks' ? '' : 'active'}"
            data-action="switch-view"
            data-view="maps"
          >
            Maps
          </button>
          <button
            type="button"
            class="nav-tab ${state.currentView === 'tasks' ? 'active' : ''}"
            data-action="switch-view"
            data-view="tasks"
          >
            Tasks
          </button>
        </div>
        <div class="nav-actions">
          <button type="button" class="secondary-button" data-action="refresh-maps">Refresh from GitHub</button>
        </div>
      </nav>
      ${viewMarkup}
    </section>
  `;
}

function renderSettingsPanel() {
  if (!ui.settingsRoot) {
    return;
  }

  if (!state.showSettings || state.authState !== 'authenticated') {
    ui.settingsRoot.hidden = true;
    ui.settingsRoot.innerHTML = '';
    return;
  }

  ui.settingsRoot.hidden = false;
  renderSettingsScreen({
    mountNode: ui.settingsRoot,
    repoSettings: state.repoSettings,
    hasToken: Boolean(getToken(state.repoSettings.tokenStorageKey)),
    syncMetadata: getSyncMetadata(),
    onSaveSettings: async (nextSettings) => {
      const normalizedSettings = normalizeRepoSettings({
        ...nextSettings,
        tokenStorageKey: state.runtimeConfig?.auth?.tokenStorageKey || state.repoSettings.tokenStorageKey,
      });
      saveRepoSettings(normalizedSettings, state.runtimeConfig?.auth?.settingsStorageKey);
      switchRepoContext(normalizedSettings);
      state.showSettings = false;
      const token = getToken(state.repoSettings.tokenStorageKey);
      if (!token) {
        state.authState = 'needsToken';
        state.syncState = {
          kind: 'idle',
          tone: 'warning',
          message: 'Repository settings were saved. Enter a token to reconnect.',
          detail: describeRepoSettings(state.repoSettings),
          canRetry: false,
        };
        render();
        return;
      }

      await authenticateAndLoad();
    },
    onClearToken: () => {
      clearToken(state.repoSettings.tokenStorageKey);
      state.service = null;
      state.showSettings = false;
      state.authState = 'needsToken';
      state.tokenError = '';
      state.syncState = {
        kind: 'idle',
        tone: 'warning',
        message: 'Saved token cleared. Enter a new token to reconnect.',
        detail: describeRepoSettings(state.repoSettings),
        canRetry: false,
      };
      render();
    },
    onRevalidate: () => {
      state.showSettings = false;
      void authenticateAndLoad();
    },
    onClose: () => {
      state.showSettings = false;
      render();
    },
  });
}

function renderMapsView(summaries) {
  if (summaries.length === 0) {
    return `
      <section class="workspace-panel empty-panel">
        <div class="section-heading">
          <div>
            <p class="eyebrow">Maps</p>
            <h2>Mind map files</h2>
            <p class="section-copy">The configured FocusMaps folder is reachable, but no top-level .json map files were found.</p>
          </div>
        </div>
        <article class="card empty-card">
          <p>Point <code>repoPath</code> directly at the FocusMaps folder, for example <code>Tool/PMMT/FocusMaps</code>, and make sure the folder contains map JSON files.</p>
        </article>
      </section>
    `;
  }

  return `
    <section class="workspace-panel">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Maps</p>
          <h2>Mind map files</h2>
          <p class="section-copy">Open any existing map and edit the task tree without changing unsupported mind-map data.</p>
        </div>
      </div>
      <div class="map-grid">
        ${summaries.map((summary) => renderMapCard(summary)).join('')}
      </div>
    </section>
  `;
}

function renderMapCard(summary) {
  const pendingCount = getPendingCountForMap(summary.filePath);
  const countItems = [
    `Open ${summary.taskCounts.open}`,
    `Todo ${summary.taskCounts.todo}`,
    `Doing ${summary.taskCounts.doing}`,
    `Done ${summary.taskCounts.done}`,
  ];

  if (pendingCount > 0) {
    countItems.push(`Pending ${pendingCount}`);
  }

  return `
    <article class="card map-card">
      <div class="map-card-header">
        <div>
          <h3>${escapeHtml(summary.rootTitle)}</h3>
          <p class="map-file-name">${escapeHtml(summary.fileName)}</p>
        </div>
        <button type="button" data-action="open-map" data-map-path="${escapeHtml(summary.filePath)}">Open map</button>
      </div>
      <p class="map-updated">Updated ${escapeHtml(formatRelativeTime(summary.updatedAt))}</p>
      <div class="pill-row">
        ${countItems.map((item) => `<span class="pill">${escapeHtml(item)}</span>`).join('')}
      </div>
    </article>
  `;
}

function renderMapView() {
  const snapshot = getSelectedSnapshot();
  if (!snapshot) {
    state.currentView = 'maps';
    return renderMapsView(getSnapshots().map((item) => buildMapSummary(item)));
  }

  const selectedNodeState = getSelectedNodeUiState(snapshot);
  if (!selectedNodeState) {
    return `
      <section class="workspace-panel empty-panel">
        <article class="card empty-card">
          <p>The selected node is no longer available. Return to the maps list and reopen the map.</p>
          <button type="button" class="secondary-button" data-action="back-to-maps">Back to maps</button>
        </article>
      </section>
    `;
  }

  return `
    <section class="workspace-panel map-editor">
      <div class="map-editor-header">
        <div>
          <p class="eyebrow">Map editor</p>
          <h2>${escapeHtml(snapshot.mapName)}</h2>
          <p class="section-copy">${escapeHtml(snapshot.filePath)}</p>
        </div>
        <button type="button" class="secondary-button" data-action="back-to-maps">Back to maps</button>
      </div>
      <div class="editor-layout">
        <section class="card tree-panel" aria-label="Map tree">
          <ul class="tree-list">
            ${renderTreeNode(snapshot, snapshot.document.rootNode, 0)}
          </ul>
        </section>
        <aside class="card inspector-panel" aria-label="Selected node editor">
          ${renderInspector(snapshot, selectedNodeState)}
        </aside>
      </div>
    </section>
  `;
}

function renderTreeNode(snapshot, node, depth) {
  const isSelected =
    state.selectedMapPath === snapshot.filePath &&
    state.selectedNodeId === node.uniqueIdentifier;
  const hasChildren = Array.isArray(node.children) && node.children.length > 0;
  const isCollapsed = getLocalCollapsedState(snapshot.filePath, node);
  const taskMarker = displayTaskState(node.taskState);
  const badges = getNodeBadges(node);
  const markerLabel = taskMarker ? `<span class="task-marker">${escapeHtml(taskMarker)}</span>` : '';

  return `
    <li class="tree-node ${isSelected ? 'selected' : ''}" style="--depth:${depth}">
      <div class="tree-row">
        ${hasChildren
          ? `<button
              type="button"
              class="tree-toggle"
              data-action="toggle-node"
              data-map-path="${escapeHtml(snapshot.filePath)}"
              data-node-id="${escapeHtml(node.uniqueIdentifier)}"
              aria-label="${isCollapsed ? 'Expand' : 'Collapse'} node"
            >${isCollapsed ? '+' : '-'}</button>`
          : '<span class="tree-spacer" aria-hidden="true"></span>'}
        <button
          type="button"
          class="tree-label ${isSelected ? 'selected' : ''}"
          data-action="select-node"
          data-map-path="${escapeHtml(snapshot.filePath)}"
          data-node-id="${escapeHtml(node.uniqueIdentifier)}"
        >
          ${markerLabel}
          <span>${escapeHtml(node.name || '(untitled)')}</span>
        </button>
        ${badges.length > 0 ? `<span class="badge-row">${renderBadgeMarkup(badges)}</span>` : ''}
      </div>
      ${hasChildren && !isCollapsed
        ? `<ul class="tree-list child-list">${node.children.map((child) => renderTreeNode(snapshot, child, depth + 1)).join('')}</ul>`
        : ''}
    </li>
  `;
}

function renderInspector(snapshot, nodeUiState) {
  const selectionPath = nodeUiState.pathSegments.join(' > ');
  const readOnlyMessage = nodeUiState.canEditNode
    ? 'Unsupported map data such as links, attachments, idea tags, and stored collapsed flags will be preserved when you save supported edits.'
    : 'This node type is read-only in the PWA. You can inspect it here, but text and task changes stay disabled.';

  return `
    <div class="inspector-copy">
      <p class="eyebrow">Selected node</p>
      <h3>${escapeHtml(nodeUiState.node.name || '(untitled)')}</h3>
      <p class="node-path">${escapeHtml(selectionPath)}</p>
      <div class="pill-row">${renderBadgeMarkup(nodeUiState.badges)}</div>
      <p class="section-copy">${escapeHtml(describeSelectionCapabilities(nodeUiState))}</p>
    </div>

    <form id="edit-node-form" class="stack-form inspector-form">
      <label>
        <span>Node text</span>
        <textarea name="text" rows="4" maxlength="5000" ${nodeUiState.canEditNode ? '' : 'disabled'}>${escapeHtml(nodeUiState.node.name || '')}</textarea>
      </label>
      <div class="form-actions">
        <button type="submit" ${nodeUiState.canEditNode ? '' : 'disabled'}>Save text</button>
      </div>
    </form>

    <section class="inspector-section">
      <h4>Task state</h4>
      <div class="state-row">
        ${renderTaskStateButton('None', TASK_STATE.NONE, nodeUiState)}
        ${renderTaskStateButton('Todo', TASK_STATE.TODO, nodeUiState)}
        ${renderTaskStateButton('Doing', TASK_STATE.DOING, nodeUiState)}
        ${renderTaskStateButton('Done', TASK_STATE.DONE, nodeUiState)}
      </div>
    </section>

    <section class="inspector-section">
      <h4>Add child note</h4>
      <form id="add-note-form" class="stack-form compact-form">
        <label>
          <span>Child note text</span>
          <input name="text" type="text" maxlength="500" placeholder="Add a child note" ${nodeUiState.canEditNode ? '' : 'disabled'} />
        </label>
        <div class="form-actions">
          <button type="submit" ${nodeUiState.canEditNode ? '' : 'disabled'}>Add note</button>
        </div>
      </form>
    </section>

    <section class="inspector-section">
      <h4>Add child task</h4>
      <form id="add-task-form" class="stack-form compact-form">
        <label>
          <span>Child task text</span>
          <input name="text" type="text" maxlength="500" placeholder="Add a child task" ${nodeUiState.canEditNode ? '' : 'disabled'} />
        </label>
        <div class="form-actions">
          <button type="submit" ${nodeUiState.canEditNode ? '' : 'disabled'}>Add task</button>
        </div>
      </form>
    </section>

    <p class="security-note">${escapeHtml(readOnlyMessage)}</p>
  `;
}

function renderTaskStateButton(label, taskState, nodeUiState) {
  const isActive = nodeUiState.node.taskState === taskState;
  const disabled = nodeUiState.canChangeTaskState ? '' : 'disabled';
  return `
    <button
      type="button"
      class="state-button ${isActive ? 'active' : ''}"
      data-action="set-task-state"
      data-task-state="${taskState}"
      ${disabled}
    >
      ${escapeHtml(label)}
    </button>
  `;
}

function renderTasksView() {
  const entries = buildTaskEntriesForView(state.taskFilter);
  const filterButtons = [
    ['open', 'Open'],
    ['todo', 'Todo'],
    ['doing', 'Doing'],
    ['done', 'Done'],
    ['all', 'All'],
  ];

  return `
    <section class="workspace-panel">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Tasks</p>
          <h2>All task nodes</h2>
          <p class="section-copy">Browse task nodes across every loaded map and jump straight back into the owning tree.</p>
        </div>
      </div>
      <div class="filter-row">
        ${filterButtons.map(([value, label]) => `
          <button
            type="button"
            class="filter-pill ${state.taskFilter === value ? 'active' : ''}"
            data-action="set-task-filter"
            data-filter="${value}"
          >
            ${escapeHtml(label)}
          </button>
        `).join('')}
      </div>
      ${entries.length === 0
        ? `
          <article class="card empty-card">
            <p>No task nodes matched the current filter.</p>
          </article>
        `
        : `
          <div class="task-entry-list">
            ${entries.map((entry) => `
              <article class="card task-entry">
                <div>
                  <p class="task-entry-map">${escapeHtml(entry.mapName)}</p>
                  <h3>${escapeHtml(entry.nodeName)}</h3>
                  <p class="task-entry-path">${escapeHtml(entry.nodePath)}</p>
                </div>
                <div class="task-entry-actions">
                  <span class="pill">${escapeHtml(taskStateLabel(entry.taskState))}</span>
                  <button
                    type="button"
                    data-action="open-task-node"
                    data-map-path="${escapeHtml(entry.filePath)}"
                    data-node-id="${escapeHtml(entry.nodeId)}"
                  >
                    Open in map
                  </button>
                </div>
              </article>
            `).join('')}
          </div>
        `}
    </section>
  `;
}

function buildTaskEntriesForView(filter) {
  return getSnapshots()
    .flatMap((snapshot) => collectTaskEntries(snapshot, filter))
    .sort((left, right) => {
      const priorityDelta = taskStatePriority(left.taskState) - taskStatePriority(right.taskState);
      if (priorityDelta !== 0) {
        return priorityDelta;
      }

      const mapDelta = left.mapName.localeCompare(right.mapName);
      if (mapDelta !== 0) {
        return mapDelta;
      }

      return left.nodePath.localeCompare(right.nodePath);
    });
}

function describeSelectionCapabilities(nodeUiState) {
  if (!nodeUiState.canEditNode) {
    return 'Idea-tag nodes are preserved but not editable in the PWA.';
  }

  if (!nodeUiState.canChangeTaskState) {
    return 'Root node text can be edited, but task state is disabled on the root node.';
  }

  return 'You can edit text, add child notes or tasks, and change task state for this node.';
}

function renderBadgeMarkup(badges) {
  if (!Array.isArray(badges) || badges.length === 0) {
    return '';
  }

  return badges.map((badge) => `<span class="pill subtle">${escapeHtml(badge)}</span>`).join('');
}

function renderStatusDetailPrefix() {
  const parts = [];
  if (isRepoSettingsComplete(state.repoSettings)) {
    parts.push(describeRepoSettings(state.repoSettings));
  }

  if (state.pendingOperations.length > 0) {
    parts.push(`${state.pendingOperations.length} pending change${state.pendingOperations.length === 1 ? '' : 's'}`);
  }

  return parts;
}

function buildStatusDetail() {
  const syncMetadata = getSyncMetadata();
  const parts = renderStatusDetailPrefix();

  if (syncMetadata.lastSyncAt) {
    parts.push(`Last sync ${formatDateTime(syncMetadata.lastSyncAt)}`);
  }

  if (syncMetadata.lastErrorSummary && state.syncState.kind !== 'success') {
    parts.push(truncate(syncMetadata.lastErrorSummary, 180));
  }

  return parts.join(' | ');
}

function buildErrorDetail(error) {
  const parts = renderStatusDetailPrefix();

  if (error?.contextLabel) {
    parts.push(`Step ${error.contextLabel}`);
  }

  if (typeof error?.status === 'number') {
    parts.push(`HTTP ${error.status}`);
  }

  if (error?.code) {
    parts.push(`Code ${error.code}`);
  }

  const responseText = typeof error?.responseText === 'string' ? error.responseText.trim() : '';
  if (responseText) {
    parts.push(truncate(responseText.replace(/\s+/g, ' '), 180));
  }

  return parts.join(' | ');
}

function describeOperation(operation) {
  const mapLabel = getMapLabel(operation.filePath);

  switch (operation.type) {
    case 'editNodeText':
      return `text change in ${mapLabel}`;
    case 'setTaskState':
      return `task state update in ${mapLabel}`;
    case 'addChildTask':
      return `new child task in ${mapLabel}`;
    case 'addChildNote':
      return `new child note in ${mapLabel}`;
    default:
      return `change in ${mapLabel}`;
  }
}

function getMapLabel(filePath) {
  return state.mapsByPath[filePath]?.mapName || filePath.split('/').pop() || 'map';
}

function getPendingCountForMap(filePath) {
  return state.pendingOperations.filter((operation) => operation.filePath === filePath).length;
}

function taskStatePriority(taskState) {
  switch (taskState) {
    case TASK_STATE.DOING:
      return 0;
    case TASK_STATE.TODO:
      return 1;
    case TASK_STATE.DONE:
      return 2;
    default:
      return 3;
  }
}

function taskStateLabel(taskState) {
  switch (taskState) {
    case TASK_STATE.TODO:
      return 'Todo';
    case TASK_STATE.DOING:
      return 'Doing';
    case TASK_STATE.DONE:
      return 'Done';
    default:
      return 'None';
  }
}

function formatRelativeTime(value) {
  if (!value) {
    return 'unknown';
  }

  const timestamp = new Date(value).getTime();
  if (Number.isNaN(timestamp)) {
    return value;
  }

  const deltaMinutes = Math.round((Date.now() - timestamp) / 60000);
  if (Math.abs(deltaMinutes) < 1) {
    return 'just now';
  }

  if (Math.abs(deltaMinutes) < 60) {
    return `${Math.abs(deltaMinutes)} minute${Math.abs(deltaMinutes) === 1 ? '' : 's'} ago`;
  }

  const deltaHours = Math.round(deltaMinutes / 60);
  if (Math.abs(deltaHours) < 48) {
    return `${Math.abs(deltaHours)} hour${Math.abs(deltaHours) === 1 ? '' : 's'} ago`;
  }

  const deltaDays = Math.round(deltaHours / 24);
  return `${Math.abs(deltaDays)} day${Math.abs(deltaDays) === 1 ? '' : 's'} ago`;
}

function formatDateTime(value) {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? String(value ?? '') : parsed.toLocaleString();
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function truncate(value, maxLength) {
  return value.length > maxLength ? `${value.slice(0, maxLength - 3)}...` : value;
}
