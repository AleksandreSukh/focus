import {
  buildConflictResolveCommitMessage,
  buildMapCreateCommitMessage,
  buildMapDeleteCommitMessage,
  buildNodeAddCommitMessage,
  buildNodeDeleteCommitMessage,
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
  findNodeRecord,
  getNodeBadges,
  getNodeUiState,
  NODE_TYPE,
  normalizeNodeDisplayText,
  nowIso,
  TASK_STATE,
} from './maps/model.js';
import { renderInlineHtml } from './formatting/inlineFormatter.js';

const THEME_STORAGE_KEY = 'focus.pwa.theme';
const FAB_SIDE_STORAGE_KEY = 'focus.pwa.fabSide';
const THEME_META_COLORS = {
  light: '#ffffff',
  dark: '#000000',
};
const HASH_ROUTE = {
  maps: '#maps',
  tasks: '#tasks',
};

const state = {
  runtimeConfig: null,
  repoSettings: null,
  repoScope: '',
  theme: 'light',
  fabSide: 'right',
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
  showStatus: false,
  showSettings: false,
  processingQueue: false,
  currentView: 'maps',
  taskFilter: 'open',
  selectedMapPath: '',
  selectedNodeId: '',
  mapsByPath: {},
  pendingOperations: [],
  localCollapsedByMap: {},
  activeModal: null,
  pendingFocusRequest: null,
  openCardMenu: '',
};

const ui = {};
let globalUiListenersBound = false;
let hashRoutingBound = false;

export async function bootstrapApp() {
  wireUi();
  registerPwaShell();

  state.theme = loadThemePreference();
  state.fabSide = loadFabSidePreference();
  applyThemePreference();
  state.runtimeConfig = resolveRuntimeConfig();
  switchRepoContext(getEffectiveRepoSettings(state.runtimeConfig));
  updateInstallState();
  initializeHashRouting();

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
  ui.themeRoot = document.getElementById('theme-root');
  ui.refreshToggle = document.getElementById('refresh-toggle');
  ui.statusToggle = document.getElementById('status-toggle');
  ui.settingsToggle = document.getElementById('settings-toggle');
  ui.screenRoot = document.getElementById('screen-root');
  ui.appRoot = document.getElementById('app-root');
  ui.statusRoot = document.getElementById('status-root');
  ui.settingsRoot = document.getElementById('settings-root');
  ui.themeMeta = document.querySelector('meta[name="theme-color"]');

  bindGlobalUiListeners();

  ui.statusToggle?.addEventListener('click', () => {
    const openingStatus = !state.showStatus;
    if (openingStatus) {
      state.activeModal = null;
      state.showSettings = false;
      state.pendingFocusRequest = {
        type: 'modalAutofocus',
      };
    } else {
      state.pendingFocusRequest = {
        type: 'focusKey',
        value: 'status-toggle',
      };
    }
    state.showStatus = openingStatus;
    render();
  });

  ui.settingsToggle?.addEventListener('click', () => {
    const openingSettings = !state.showSettings;
    if (openingSettings) {
      state.activeModal = null;
      state.showStatus = false;
      state.pendingFocusRequest = {
        type: 'modalAutofocus',
      };
    } else {
      state.pendingFocusRequest = {
        type: 'focusKey',
        value: 'settings-toggle',
      };
    }
    state.showSettings = openingSettings;
    render();
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

function initializeHashRouting() {
  ensureDefaultHashRoute();

  if (hashRoutingBound) {
    return;
  }

  window.addEventListener('hashchange', () => {
    if (state.authState !== 'authenticated') {
      return;
    }

    applyRouteFromHash({ replaceInvalid: true });
  });

  hashRoutingBound = true;
}

function ensureDefaultHashRoute() {
  if (!window.location.hash || window.location.hash === '#') {
    replaceHashRoute(HASH_ROUTE.maps);
  }
}

function replaceHashRoute(nextHash) {
  const currentPath = `${window.location.pathname}${window.location.search}`;
  window.history.replaceState(window.history.state, '', `${currentPath}${nextHash}`);
}

function buildHashRoute(view, mapPath = '') {
  if (view === 'tasks') {
    return HASH_ROUTE.tasks;
  }

  if (view === 'map' && mapPath) {
    return `#map/${encodeURIComponent(mapPath)}`;
  }

  return HASH_ROUTE.maps;
}

function parseHashRoute(hashValue) {
  const normalizedHash = typeof hashValue === 'string' ? hashValue : '';
  const routeText = normalizedHash.startsWith('#') ? normalizedHash.slice(1) : normalizedHash;

  if (!routeText || routeText === 'maps') {
    return { view: 'maps', isInvalid: false };
  }

  if (routeText === 'tasks') {
    return { view: 'tasks', isInvalid: false };
  }

  if (routeText.startsWith('map/')) {
    const encodedPath = routeText.slice(4);
    if (!encodedPath) {
      return { view: 'maps', isInvalid: true };
    }

    try {
      return {
        view: 'map',
        mapPath: decodeURIComponent(encodedPath),
        isInvalid: false,
      };
    } catch (error) {
      return { view: 'maps', isInvalid: true };
    }
  }

  return { view: 'maps', isInvalid: true };
}

function applyRouteFromHash(options = {}) {
  if (state.authState !== 'authenticated') {
    return false;
  }

  const { replaceInvalid = false } = options;
  const route = parseHashRoute(window.location.hash);

  state.activeModal = null;
  state.showStatus = false;
  state.showSettings = false;

  if (route.view === 'map') {
    const snapshot = state.mapsByPath[route.mapPath];
    if (!snapshot) {
      if (replaceInvalid) {
        replaceHashRoute(HASH_ROUTE.maps);
      }
      state.currentView = 'maps';
      render();
      return false;
    }

    state.currentView = 'map';
    state.selectedMapPath = route.mapPath;
    if (!findNodeRecord(snapshot.document, state.selectedNodeId)) {
      state.selectedNodeId = snapshot.document.rootNode?.uniqueIdentifier || '';
    }
    render();
    return true;
  }

  if (route.isInvalid && replaceInvalid) {
    replaceHashRoute(HASH_ROUTE.maps);
  }

  state.currentView = route.view === 'tasks' ? 'tasks' : 'maps';
  render();
  return true;
}

function navigateToHashRoute(nextHash, options = {}) {
  const { replace = false } = options;
  if (window.location.hash === nextHash) {
    applyRouteFromHash({ replaceInvalid: false });
    return;
  }

  if (replace) {
    replaceHashRoute(nextHash);
    applyRouteFromHash({ replaceInvalid: false });
    return;
  }

  window.location.hash = nextHash;
}

function navigateToMapsRoute(options = {}) {
  navigateToHashRoute(HASH_ROUTE.maps, options);
}

function navigateToTasksRoute(options = {}) {
  navigateToHashRoute(HASH_ROUTE.tasks, options);
}

function navigateToMapRoute(mapPath, options = {}) {
  const snapshot = state.mapsByPath[mapPath];
  if (!snapshot) {
    return;
  }

  const { replace = false, preferredNodeId = '' } = options;
  const nextHash = buildHashRoute('map', mapPath);

  state.selectedMapPath = mapPath;
  if (preferredNodeId && findNodeRecord(snapshot.document, preferredNodeId)) {
    state.selectedNodeId = preferredNodeId;
  } else if (!findNodeRecord(snapshot.document, state.selectedNodeId)) {
    state.selectedNodeId = snapshot.document.rootNode?.uniqueIdentifier || '';
  }

  if (window.location.hash !== HASH_ROUTE.maps) {
    replaceHashRoute(HASH_ROUTE.maps);
  }

  navigateToHashRoute(nextHash, { replace });
}

function bindGlobalUiListeners() {
  if (globalUiListenersBound) {
    return;
  }

  document.addEventListener('submit', handleDocumentSubmit, true);
  document.addEventListener('mousedown', handleDocumentMouseDown, true);
  document.addEventListener('click', handleDocumentClick, true);
  document.addEventListener('keydown', handleDocumentKeydown, true);
  document.addEventListener('change', handleDocumentChange, true);
  document.addEventListener('input', handleDocumentInput, true);
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
    case 'create-map-form':
      event.preventDefault();
      await handleCreateMapSubmit(form);
      return;
    default:
      return;
  }
}

function handleDocumentMouseDown(event) {
  if (!(event.target instanceof Element)) {
    return;
  }

  const actionElement = event.target.closest('[data-action="select-node"]');
  if (!(actionElement instanceof HTMLElement)) {
    return;
  }

  // Prevent the browser from setting focus on mousedown — this eliminates the
  // one-frame gap where the focus ring would appear before the click handler runs.
  event.preventDefault();
  selectNode(actionElement.dataset.mapPath || '', actionElement.dataset.nodeId || '');
}

function handleDocumentClick(event) {
  const target = event.target;
  if (!(target instanceof Element)) {
    return;
  }

  if (state.openCardMenu && !target.closest('.card-menu')) {
    state.openCardMenu = '';
    render();
  }

  const nestedLink = target.closest('a[href]');
  if (nestedLink instanceof HTMLAnchorElement) {
    return;
  }

  const button = target.closest('button');
  const actionElement = target.closest('[data-action]');
  const clickableElement = actionElement instanceof HTMLElement ? actionElement : null;

  if (button instanceof HTMLButtonElement) {
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
  }

  if (!clickableElement?.dataset.action) {
    return;
  }

  event.preventDefault();
  switch (clickableElement.dataset.action) {
    case 'switch-view':
      if (clickableElement.dataset.view === 'tasks') {
        navigateToTasksRoute();
        return;
      }

      navigateToMapsRoute();
      return;
    case 'refresh-maps':
      void loadWorkspace(true);
      return;
    case 'open-map':
      openMap(clickableElement.dataset.mapPath || '');
      return;
    case 'back-to-maps':
      navigateToMapsRoute({ replace: true });
      return;
    case 'select-node':
      // Mouse clicks are handled in handleDocumentMouseDown; only handle keyboard-triggered clicks here.
      if (event.detail === 0) {
        selectNode(clickableElement.dataset.mapPath || '', clickableElement.dataset.nodeId || '');
      }
      return;
    case 'toggle-node':
      toggleLocalNodeCollapse(clickableElement.dataset.mapPath || '', clickableElement.dataset.nodeId || '');
      return;
    case 'set-task-state':
      void handleSetTaskState(
        clickableElement.dataset.taskState || '',
        clickableElement.dataset.mapPath,
        clickableElement.dataset.nodeId,
      );
      return;
    case 'open-modal':
      openNodeModal(
        clickableElement.dataset.modalKind || '',
        clickableElement.dataset.mapPath || '',
        clickableElement.dataset.nodeId || '',
        clickableElement.dataset.focusKey || '',
      );
      return;
    case 'close-modal':
      closeActiveModal();
      return;
    case 'open-create-map-modal':
      openCreateMapModal(clickableElement.dataset.focusKey || '');
      return;
    case 'open-conflict-modal':
      void openConflictModal();
      return;
    case 'set-conflict-resolution': {
      const itemId = Number.parseInt(clickableElement.dataset.itemId ?? '', 10);
      const resolution = clickableElement.dataset.resolution === 'right' ? 'right' : 'left';
      if (state.activeModal?.kind === 'resolveConflict' && !Number.isNaN(itemId)) {
        state.activeModal = {
          ...state.activeModal,
          items: state.activeModal.items.map((item) =>
            item.id === itemId ? { ...item, resolution } : item,
          ),
        };
        render();
      }
      return;
    }
    case 'accept-conflict-resolution':
      void handleAcceptConflictResolution();
      return;
    case 'confirm-delete-node':
      void handleDeleteNodeConfirm();
      return;
    case 'toggle-card-menu':
      toggleCardMenu(clickableElement.dataset.mapPath || '');
      return;
    case 'open-delete-map-modal':
      state.openCardMenu = '';
      openDeleteMapModal(
        clickableElement.dataset.mapPath || '',
        clickableElement.dataset.focusKey || '',
      );
      return;
    case 'confirm-delete-map':
      void handleDeleteMapConfirm();
      return;
    case 'open-task-node':
      openTaskNode(clickableElement.dataset.mapPath || '', clickableElement.dataset.nodeId || '');
      return;
    case 'set-task-filter':
      state.taskFilter = state.taskFilter === clickableElement.dataset.filter
        ? ''
        : clickableElement.dataset.filter || 'open';
      render();
      return;
    default:
      return;
  }
}

function handleDocumentChange(event) {
  const target = event.target;
  if (!(target instanceof HTMLInputElement)) {
    return;
  }

  if (target.name !== 'theme-mode') {
    return;
  }

  const nextTheme = target.value === 'dark' ? 'dark' : 'light';
  if (state.theme === nextTheme) {
    return;
  }

  state.theme = nextTheme;
  saveThemePreference(nextTheme);
  applyThemePreference();
  render();
}

function handleDocumentInput(event) {
  const target = event.target;
  if (!(target instanceof HTMLInputElement) && !(target instanceof HTMLTextAreaElement)) {
    return;
  }

  const form = target.closest('form');
  if (!(form instanceof HTMLFormElement)) {
    return;
  }

  if (state.activeModal && ['edit-node-form', 'add-note-form', 'add-task-form', 'create-map-form'].includes(form.id)) {
    state.activeModal = {
      ...state.activeModal,
      draftText: target.value,
      errorMessage: '',
    };
  }

  if (form.id !== 'edit-node-form') {
    return;
  }

  const previewNode = form.querySelector('[data-inline-preview="edit-node"]');
  if (!(previewNode instanceof HTMLElement)) {
    return;
  }

  const modalNodeState = getActiveModalContext('editNode')?.nodeUiState ?? null;
  const taskState = modalNodeState?.node.taskState ?? TASK_STATE.NONE;
  previewNode.innerHTML = renderNodePreviewMarkup(target.value, taskState);
}

function handleDocumentKeydown(event) {
  const target = event.target;
  if (!(target instanceof HTMLElement)) {
    return;
  }

  if (event.key === 'Escape' && state.activeModal) {
    event.preventDefault();
    closeActiveModal();
    return;
  }

  if (event.key === 'Escape' && state.showSettings) {
    event.preventDefault();
    state.pendingFocusRequest = {
      type: 'focusKey',
      value: 'settings-toggle',
    };
    state.showSettings = false;
    render();
    return;
  }

  if (event.key === 'Escape' && state.showStatus) {
    event.preventDefault();
    state.pendingFocusRequest = {
      type: 'focusKey',
      value: 'status-toggle',
    };
    state.showStatus = false;
    render();
    return;
  }

  if (target.closest('a[href]')) {
    return;
  }

  if (event.key !== 'Enter' && event.key !== ' ') {
    return;
  }

  const actionElement = target.closest('[data-action][role="button"]');
  if (!(actionElement instanceof HTMLElement)) {
    return;
  }

  event.preventDefault();
  actionElement.click();
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

function loadThemePreference() {
  const storedTheme = window.localStorage?.getItem(THEME_STORAGE_KEY);
  return storedTheme === 'dark' ? 'dark' : 'light';
}

function saveThemePreference(theme) {
  window.localStorage?.setItem(THEME_STORAGE_KEY, theme === 'dark' ? 'dark' : 'light');
}

function loadFabSidePreference() {
  const stored = window.localStorage?.getItem(FAB_SIDE_STORAGE_KEY);
  return stored === 'left' ? 'left' : 'right';
}

function saveFabSidePreference(side) {
  window.localStorage?.setItem(FAB_SIDE_STORAGE_KEY, side === 'left' ? 'left' : 'right');
}

function applyThemePreference() {
  document.documentElement.dataset.theme = state.theme;

  if (ui.themeMeta instanceof HTMLMetaElement) {
    ui.themeMeta.content = THEME_META_COLORS[state.theme] || THEME_META_COLORS.light;
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
  state.showStatus = false;
  state.showSettings = false;
  state.localCollapsedByMap = {};
  state.activeModal = null;
  state.pendingFocusRequest = null;
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
  applyRouteFromHash({ replaceInvalid: true });

  if (state.pendingOperations.length > 0) {
    void processPendingOperations();
  }
}

function enqueueOperation(operation) {
  const currentSnapshot = state.mapsByPath[operation.filePath];
  if (!currentSnapshot) {
    return {
      ok: false,
      error: {
        message: 'The selected map is no longer available.',
      },
    };
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
    return applied;
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
  return {
    ok: true,
    value: applied.value,
  };
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
  const modalContext = getActiveModalContext('editNode');
  if (!modalContext) {
    return;
  }

  const text = String(new FormData(form).get('text') ?? '').trim();
  state.activeModal = {
    ...state.activeModal,
    draftText: text,
    errorMessage: '',
  };

  const operation = {
    type: 'editNodeText',
    filePath: modalContext.snapshot.filePath,
    nodeId: modalContext.nodeUiState.node.uniqueIdentifier,
    text,
    timestamp: nowIso(),
    commitMessage: buildNodeEditCommitMessage(
      modalContext.snapshot.mapName,
      modalContext.nodeUiState.node.uniqueIdentifier,
    ),
  };

  const result = enqueueOperation(operation);
  if (!result.ok) {
    setActiveModalError(result.error.message);
    return;
  }

  closeActiveModal({
    focusKey: buildNodeFocusKey(operation.filePath, state.selectedNodeId || operation.nodeId),
  });
}

async function handleAddChildSubmit(form, type) {
  const expectedModalKind = type === 'addChildTask' ? 'addChildTask' : 'addChildNote';
  const modalContext = getActiveModalContext(expectedModalKind);
  if (!modalContext) {
    return;
  }

  const text = String(new FormData(form).get('text') ?? '').trim();
  state.activeModal = {
    ...state.activeModal,
    draftText: text,
    errorMessage: '',
  };

  const operation = {
    type,
    filePath: modalContext.snapshot.filePath,
    parentNodeId: modalContext.nodeUiState.node.uniqueIdentifier,
    newNodeId: createClientNodeId(),
    text,
    timestamp: nowIso(),
    commitMessage: buildNodeAddCommitMessage(
      modalContext.snapshot.mapName,
      text,
      type === 'addChildTask' ? 'task' : 'note',
    ),
  };

  const result = enqueueOperation(operation);
  if (!result.ok) {
    setActiveModalError(result.error.message);
    return;
  }

  closeActiveModal({
    focusKey: buildNodeFocusKey(operation.filePath, state.selectedNodeId || operation.parentNodeId),
  });
}

async function handleSetTaskState(taskStateValue, explicitMapPath, explicitNodeId) {
  const nextTaskState = Number.parseInt(String(taskStateValue ?? ''), 10);
  if (!Number.isInteger(nextTaskState)) {
    return;
  }

  let filePath, nodeId, mapName;

  if (explicitMapPath && explicitNodeId) {
    const snapshot = state.mapsByPath[explicitMapPath];
    if (!snapshot) return;
    filePath = explicitMapPath;
    nodeId = explicitNodeId;
    mapName = snapshot.mapName;
  } else {
    const snapshot = getSelectedSnapshot();
    const selectedNode = getSelectedNodeUiState(snapshot);
    if (!snapshot || !selectedNode) return;
    filePath = snapshot.filePath;
    nodeId = selectedNode.node.uniqueIdentifier;
    mapName = snapshot.mapName;
  }

  const result = enqueueOperation({
    type: 'setTaskState',
    filePath,
    nodeId,
    taskState: nextTaskState,
    timestamp: nowIso(),
    commitMessage: buildNodeTaskStateCommitMessage(mapName, nodeId, nextTaskState),
  });

  if (result.ok) {
    state.pendingFocusRequest = {
      type: 'focusKey',
      value: buildNodeFocusKey(filePath, nodeId),
    };
    focusPendingElement();
  }
}

async function handleCreateMapSubmit(form) {
  if (!state.activeModal || state.activeModal.kind !== 'createMap') {
    return;
  }

  const rawName = String(new FormData(form).get('mapName') ?? '').trim();
  const name = rawName.replace(/\.json$/i, '').trim();

  state.activeModal = {
    ...state.activeModal,
    draftText: rawName,
    errorMessage: '',
  };

  if (!name) {
    setActiveModalError('Map name cannot be empty.');
    return;
  }

  if (name.includes('/') || name.includes('\\')) {
    setActiveModalError('Map name cannot contain "/" or "\\".');
    return;
  }

  const existingNames = getSnapshots().map((s) => s.mapName.toLowerCase());
  if (existingNames.includes(name.toLowerCase())) {
    setActiveModalError(`A map named "${name}" already exists.`);
    return;
  }

  const repoPath = state.repoSettings?.repoPath
    ? String(state.repoSettings.repoPath).replace(/\/+$/, '')
    : '';
  const filePath = repoPath ? `${repoPath}/${name}.json` : `${name}.json`;

  const result = await state.service.createMap(filePath, name, buildMapCreateCommitMessage(name));
  if (!result.ok) {
    setActiveModalError(result.error.message || 'Failed to create map. Try again.');
    return;
  }

  replaceSnapshot(result.value);
  state.service.hydrateSnapshots(getSnapshots());
  persistRepoScopedState();
  state.syncState = {
    kind: 'success',
    tone: 'success',
    message: `Map "${name}" created.`,
    detail: buildStatusDetail(),
    canRetry: false,
  };
  state.activeModal = null;
  navigateToMapRoute(filePath);
}

async function handleDeleteNodeConfirm() {
  const modalContext = getActiveModalContext('deleteNode');
  if (!modalContext) {
    return;
  }

  const { snapshot, nodeUiState } = modalContext;
  const parentId = nodeUiState.parent?.uniqueIdentifier;
  if (!parentId) {
    setActiveModalError('Cannot delete the root node.');
    return;
  }

  const operation = {
    type: 'deleteNode',
    filePath: snapshot.filePath,
    nodeId: nodeUiState.node.uniqueIdentifier,
    timestamp: nowIso(),
    commitMessage: buildNodeDeleteCommitMessage(
      snapshot.mapName,
      nodeUiState.node.uniqueIdentifier,
    ),
  };

  const result = enqueueOperation(operation);
  if (!result.ok) {
    setActiveModalError(result.error.message);
    return;
  }

  closeActiveModal({
    focusKey: buildNodeFocusKey(snapshot.filePath, parentId),
  });
}

function toggleCardMenu(mapPath) {
  state.openCardMenu = state.openCardMenu === mapPath ? '' : mapPath;
  render();
}

function openDeleteMapModal(mapPath, returnFocusKey = '') {
  const snapshot = state.mapsByPath[mapPath];
  if (!snapshot) {
    return;
  }

  state.activeModal = {
    kind: 'deleteMap',
    mapPath,
    nodeId: '',
    draftText: '',
    errorMessage: '',
    returnFocusKey: returnFocusKey || 'maps-list',
  };
  state.pendingFocusRequest = {
    type: 'modalAutofocus',
  };
  render();
}

async function handleDeleteMapConfirm() {
  if (!state.activeModal || state.activeModal.kind !== 'deleteMap') {
    return;
  }

  const filePath = state.activeModal.mapPath;
  const snapshot = state.mapsByPath[filePath];
  if (!snapshot) {
    setActiveModalError('Map is no longer available.');
    return;
  }

  const commitMessage = buildMapDeleteCommitMessage(snapshot.mapName);
  const result = await state.service.deleteMap(filePath, commitMessage);
  if (!result.ok) {
    setActiveModalError(result.error.message || 'Failed to delete map. Try again.');
    return;
  }

  setSnapshots(getSnapshots().filter((item) => item.filePath !== filePath));
  state.service.hydrateSnapshots(getSnapshots());
  persistRepoScopedState();
  state.syncState = {
    kind: 'success',
    tone: 'success',
    message: `Map "${snapshot.mapName}" deleted.`,
    detail: buildStatusDetail(),
    canRetry: false,
  };
  state.activeModal = null;
  state.currentView = 'maps';
  render();
}

async function openConflictModal() {
  const failingOp = state.pendingOperations[0];
  if (!failingOp || state.syncState.kind !== 'conflict') {
    return;
  }

  const filePath = failingOp.filePath;
  const mapName = getMapLabel(filePath);
  const opsForMap = state.pendingOperations.filter((op) => op.filePath === filePath);

  state.activeModal = {
    kind: 'resolveConflict',
    mapPath: '',
    nodeId: '',
    draftText: '',
    errorMessage: '',
    returnFocusKey: 'status-summary-card',
    filePath,
    mapName,
    items: opsForMap.map((op, i) => ({
      id: i,
      description: describeOperation(op),
      operation: op,
      resolution: null,
    })),
    remoteDocument: null,
    remoteRevision: null,
    loading: true,
  };
  state.showStatus = false;
  state.pendingFocusRequest = { type: 'modalAutofocus' };
  render();

  const loaded = await state.service.loadMap(filePath, true);
  if (!state.activeModal || state.activeModal.kind !== 'resolveConflict') {
    return;
  }

  if (!loaded.ok) {
    state.activeModal = {
      ...state.activeModal,
      loading: false,
      errorMessage: loaded.error?.message || 'Could not load remote document.',
    };
    render();
    return;
  }

  state.activeModal = {
    ...state.activeModal,
    loading: false,
    remoteDocument: loaded.value.document,
    remoteRevision: loaded.value.revision,
  };
  render();
}

function computeLineDiff(leftLines, rightLines) {
  const MAX = 500;
  if (leftLines.length > MAX || rightLines.length > MAX) {
    return null;
  }

  const m = leftLines.length;
  const n = rightLines.length;
  const dp = Array.from({ length: m + 1 }, () => new Array(n + 1).fill(0));

  for (let i = 1; i <= m; i++) {
    for (let j = 1; j <= n; j++) {
      dp[i][j] = leftLines[i - 1] === rightLines[j - 1]
        ? dp[i - 1][j - 1] + 1
        : Math.max(dp[i - 1][j], dp[i][j - 1]);
    }
  }

  const result = [];
  let i = m;
  let j = n;
  while (i > 0 || j > 0) {
    if (i > 0 && j > 0 && leftLines[i - 1] === rightLines[j - 1]) {
      result.push({ type: 'context', text: leftLines[i - 1] });
      i--;
      j--;
    } else if (j > 0 && (i === 0 || dp[i][j - 1] >= dp[i - 1][j])) {
      result.push({ type: 'add', text: rightLines[j - 1] });
      j--;
    } else {
      result.push({ type: 'remove', text: leftLines[i - 1] });
      i--;
    }
  }
  result.reverse();
  return result;
}

function collapseContextHunks(hunks, contextLines = 3) {
  const show = new Set();
  hunks.forEach((h, i) => {
    if (h.type !== 'context') {
      for (let d = -contextLines; d <= contextLines; d++) {
        const idx = i + d;
        if (idx >= 0 && idx < hunks.length) {
          show.add(idx);
        }
      }
    }
  });

  if (show.size === 0) {
    return [];
  }

  const result = [];
  let prevIdx = -1;
  hunks.forEach((hunk, i) => {
    if (!show.has(i)) {
      return;
    }
    if (prevIdx === -1 && i > 0) {
      result.push({ type: 'ellipsis', skipped: i });
    } else if (prevIdx !== -1 && i > prevIdx + 1) {
      result.push({ type: 'ellipsis', skipped: i - prevIdx - 1 });
    }
    result.push(hunk);
    prevIdx = i;
  });

  if (prevIdx !== -1 && prevIdx < hunks.length - 1) {
    result.push({ type: 'ellipsis', skipped: hunks.length - 1 - prevIdx });
  }

  return result;
}

function buildConflictDiffMarkup(localDocument, remoteDocument, mapName) {
  const leftText = JSON.stringify(remoteDocument, null, 2);
  const rightText = JSON.stringify(localDocument, null, 2);
  const leftLines = leftText.split('\n');
  const rightLines = rightText.split('\n');

  const hunks = computeLineDiff(leftLines, rightLines);

  if (!hunks) {
    return '<p class="card-copy diff-too-large">Document too large to display inline diff.</p>';
  }

  const hasChanges = hunks.some((h) => h.type !== 'context');
  if (!hasChanges) {
    return '<p class="card-copy">No textual differences detected between local and remote.</p>';
  }

  const collapsed = collapseContextHunks(hunks, 3);
  const lines = collapsed.map((hunk) => {
    if (hunk.type === 'ellipsis') {
      return `<div class="diff-line diff-line--ellipsis">@@ … ${hunk.skipped} unchanged line${hunk.skipped === 1 ? '' : 's'} … @@</div>`;
    }
    const prefix = hunk.type === 'add' ? '+' : hunk.type === 'remove' ? '-' : ' ';
    const cls = hunk.type === 'add' ? 'diff-line--add' : hunk.type === 'remove' ? 'diff-line--remove' : '';
    return `<div class="diff-line ${cls}">${escapeHtml(prefix + hunk.text)}</div>`;
  }).join('');

  return `
    <div class="diff-view" role="region" aria-label="Diff view">
      <div class="diff-header">
        <span class="diff-header-label diff-header-label--remove">--- remote/${escapeHtml(mapName)}.json</span>
        <span class="diff-header-label diff-header-label--add">+++ local/${escapeHtml(mapName)}.json</span>
      </div>
      <div class="diff-body">${lines}</div>
    </div>
  `;
}

function renderResolveConflictModal() {
  const modal = state.activeModal;
  if (!modal || modal.kind !== 'resolveConflict') {
    return '';
  }

  const allResolved = !modal.loading && modal.items.every((item) => item.resolution !== null);

  const localDocument = state.mapsByPath[modal.filePath]?.document ?? null;
  const diffMarkup = modal.loading
    ? '<p class="card-copy">Loading remote document…</p>'
    : localDocument && modal.remoteDocument
      ? buildConflictDiffMarkup(localDocument, modal.remoteDocument, modal.mapName)
      : '';

  const conflictRows = modal.loading
    ? ''
    : modal.items.map((item) => `
        <div class="conflict-item ${item.resolution ? 'conflict-item--resolved' : ''}">
          <p class="conflict-description">${escapeHtml(item.description)}</p>
          <div class="conflict-choices">
            <button
              type="button"
              class="secondary-button compact-button ${item.resolution === 'left' ? 'active' : ''}"
              data-action="set-conflict-resolution"
              data-item-id="${item.id}"
              data-resolution="left"
            >Take local</button>
            <button
              type="button"
              class="secondary-button compact-button ${item.resolution === 'right' ? 'active' : ''}"
              data-action="set-conflict-resolution"
              data-item-id="${item.id}"
              data-resolution="right"
            >Take remote</button>
          </div>
          ${item.resolution
            ? `<p class="conflict-resolution-label">${escapeHtml(
                item.resolution === 'left' ? 'Keep my change' : 'Use remote version',
              )}</p>`
            : ''}
        </div>
      `).join('');

  return `
    <div class="modal-layer">
      <button type="button" class="modal-backdrop" data-action="close-modal" aria-label="Close dialog"></button>
      <div class="modal-card modal-card--wide" role="dialog" aria-modal="true" aria-labelledby="map-modal-title">
        <div class="modal-header">
          <div>
            <p class="eyebrow">Sync conflict</p>
            <h3 id="map-modal-title">Resolve conflicts in ${escapeHtml(modal.mapName)}</h3>
          </div>
          <button type="button" class="ghost-button compact-button" data-action="close-modal" data-modal-autofocus="true">Close</button>
        </div>

        ${modal.errorMessage
          ? `<p class="form-error" role="alert">${escapeHtml(modal.errorMessage)}</p>`
          : ''}

        ${diffMarkup}

        ${conflictRows
          ? `<div class="conflict-list">${conflictRows}</div>`
          : ''}

        <div class="form-actions">
          <button type="button" class="secondary-button" data-action="close-modal">Cancel</button>
          <button
            type="button"
            data-action="accept-conflict-resolution"
            ${allResolved ? '' : 'disabled'}
          >Accept</button>
        </div>
      </div>
    </div>
  `;
}

async function handleAcceptConflictResolution() {
  const modal = state.activeModal;
  if (!modal || modal.kind !== 'resolveConflict') {
    return;
  }

  if (modal.items.some((item) => item.resolution === null)) {
    return;
  }

  const { filePath, mapName, items, remoteDocument, remoteRevision } = modal;

  const mergedDocument = cloneMapDocument(remoteDocument);
  for (const item of items) {
    if (item.resolution === 'left') {
      applyMapMutation(mergedDocument, item.operation);
    }
  }

  const result = await state.service.saveResolved(
    filePath,
    mergedDocument,
    remoteRevision,
    buildConflictResolveCommitMessage(mapName),
  );

  if (!state.activeModal || state.activeModal.kind !== 'resolveConflict') {
    return;
  }

  if (!result.ok) {
    state.activeModal = {
      ...state.activeModal,
      errorMessage: result.error?.message || 'Failed to save resolution. Try again.',
    };
    render();
    return;
  }

  state.pendingOperations = state.pendingOperations.filter((op) => op.filePath !== filePath);
  replaceSnapshot(result.value);
  persistRepoScopedState();

  state.syncState = {
    kind: 'success',
    tone: 'success',
    message: `Conflict in "${mapName}" resolved.`,
    detail: buildStatusDetail(),
    canRetry: false,
  };
  state.activeModal = null;
  render();

  if (state.pendingOperations.length > 0) {
    void processPendingOperations();
  }
}

function openCreateMapModal(returnFocusKey = '') {
  state.activeModal = {
    kind: 'createMap',
    mapPath: '',
    nodeId: '',
    draftText: '',
    errorMessage: '',
    returnFocusKey: returnFocusKey || 'create-map-trigger',
  };
  state.pendingFocusRequest = {
    type: 'modalAutofocus',
  };
  render();
}

function openNodeModal(kind, mapPath, nodeId, returnFocusKey = '') {
  const snapshot = state.mapsByPath[mapPath];
  if (!snapshot) {
    return;
  }

  const nodeUiState = getNodeUiState(snapshot.document, nodeId);
  if (!nodeUiState) {
    return;
  }

  state.currentView = 'map';
  state.selectedMapPath = mapPath;
  state.selectedNodeId = nodeUiState.node.uniqueIdentifier;
  state.activeModal = {
    kind,
    mapPath,
    nodeId: nodeUiState.node.uniqueIdentifier,
    draftText: kind === 'editNode' ? nodeUiState.node.name || '' : '',
    errorMessage: '',
    returnFocusKey: returnFocusKey || buildNodeFocusKey(mapPath, nodeUiState.node.uniqueIdentifier),
  };
  state.pendingFocusRequest = {
    type: 'modalAutofocus',
  };
  render();
}

function closeActiveModal(options = {}) {
  if (!state.activeModal) {
    return;
  }

  const focusKey = options.focusKey || state.activeModal.returnFocusKey || '';
  state.activeModal = null;
  if (focusKey) {
    state.pendingFocusRequest = {
      type: 'focusKey',
      value: focusKey,
    };
  }
  render();
}

function setActiveModalError(message) {
  if (!state.activeModal) {
    return;
  }

  state.activeModal = {
    ...state.activeModal,
    errorMessage: message,
  };
  render();
}

function openMap(mapPath) {
  navigateToMapRoute(mapPath);
}

function openTaskNode(mapPath, nodeId) {
  const snapshot = state.mapsByPath[mapPath];
  if (!snapshot) {
    return;
  }

  const nextNodeId = findNodeRecord(snapshot.document, nodeId)
    ? nodeId
    : snapshot.document.rootNode?.uniqueIdentifier || '';
  navigateToMapRoute(mapPath, {
    preferredNodeId: nextNodeId,
  });
}

function nodeNeedsActions(record) {
  return Boolean(
    record && (
      record.node.taskState !== TASK_STATE.NONE ||
      record.node.nodeType === NODE_TYPE.IDEA_BAG_ITEM ||
      getNodeBadges(record.node).length > 0
    ),
  );
}

function selectNode(mapPath, nodeId) {
  const snapshot = state.mapsByPath[mapPath];
  if (!snapshot) {
    return;
  }

  if (state.selectedMapPath === mapPath && state.selectedNodeId === nodeId) {
    openNodeModal('editNode', mapPath, nodeId, buildNodeFocusKey(mapPath, nodeId));
    return;
  }

  const prevNodeId = state.selectedNodeId;
  state.activeModal = null;
  state.currentView = 'map';
  state.selectedMapPath = mapPath;
  state.selectedNodeId = findNodeRecord(snapshot.document, nodeId)
    ? nodeId
    : snapshot.document.rootNode?.uniqueIdentifier || '';

  const prevRecord = prevNodeId ? findNodeRecord(snapshot.document, prevNodeId) : null;
  const nextRecord = findNodeRecord(snapshot.document, state.selectedNodeId);

  if (!nodeNeedsActions(prevRecord) && !nodeNeedsActions(nextRecord)) {
    if (prevNodeId) {
      document.querySelector(`[data-action="select-node"][data-node-id="${prevNodeId}"]`)
        ?.classList.remove('selected');
    }
    document.querySelector(`[data-action="select-node"][data-node-id="${state.selectedNodeId}"]`)
      ?.classList.add('selected');
    return;
  }

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
    state.activeModal = null;
    if (state.currentView === 'map') {
      state.currentView = 'maps';
    }
    return;
  }

  const selectedSnapshot = nextByPath[state.selectedMapPath];
  if (!findNodeRecord(selectedSnapshot.document, state.selectedNodeId)) {
    state.selectedNodeId = selectedSnapshot.document.rootNode?.uniqueIdentifier || '';
    state.activeModal = null;
  }

  if (state.activeModal && state.activeModal.mapPath) {
    const modalSnapshot = nextByPath[state.activeModal.mapPath];
    if (!modalSnapshot || !findNodeRecord(modalSnapshot.document, state.activeModal.nodeId)) {
      state.activeModal = null;
    }
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

function getNodeUiStateForLocation(mapPath, nodeId) {
  const snapshot = state.mapsByPath[mapPath];
  if (!snapshot) {
    return null;
  }

  const nodeUiState = getNodeUiState(snapshot.document, nodeId);
  if (!nodeUiState) {
    return null;
  }

  return {
    snapshot,
    nodeUiState,
  };
}

function getActiveModalContext(expectedKind = '') {
  if (!state.activeModal) {
    return null;
  }

  if (expectedKind && state.activeModal.kind !== expectedKind) {
    return null;
  }

  return getNodeUiStateForLocation(state.activeModal.mapPath, state.activeModal.nodeId);
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

function buildNodeFocusKey(mapPath, nodeId) {
  return `node|${mapPath}|${nodeId}`;
}

function focusPendingElement() {
  const request = state.pendingFocusRequest;
  if (!request) {
    return;
  }

  state.pendingFocusRequest = null;
  queueMicrotask(() => {
    let element = null;
    if (request.type === 'modalAutofocus') {
      element = document.querySelector('[data-modal-autofocus="true"]');
    } else if (request.type === 'focusKey') {
      element = Array.from(document.querySelectorAll('[data-focus-key]'))
        .find((candidate) => candidate instanceof HTMLElement && candidate.dataset.focusKey === request.value) || null;
    }

    if (element instanceof HTMLElement) {
      element.focus();
    }
  });
}

function render() {
  renderStatusPanel();
  renderHeaderThemeControl();

  if (ui.refreshToggle) {
    ui.refreshToggle.hidden = state.authState !== 'authenticated';
  }

  if (ui.statusToggle) {
    ui.statusToggle.hidden = false;
  }

  if (ui.settingsToggle) {
    ui.settingsToggle.hidden = state.authState !== 'authenticated';
  }

  if (state.authState === 'authenticated') {
    renderWorkspace();
    renderSettingsPanel();
    focusPendingElement();
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
  if (ui.statusRoot && !state.showStatus) {
    ui.statusRoot.hidden = true;
    ui.statusRoot.innerHTML = '';
  }

  renderGateScreen();
  focusPendingElement();
}

function renderHeaderThemeControl() {
  if (!ui.themeRoot) {
    return;
  }

  if (state.authState !== 'authenticated') {
    ui.themeRoot.hidden = true;
    ui.themeRoot.innerHTML = '';
    return;
  }

  ui.themeRoot.hidden = false;
  ui.themeRoot.innerHTML = renderThemeModeControl('theme-switcher--header');
}

function renderStatusPanel() {
  if (ui.statusToggle) {
    const statusLabel = `Status: ${state.syncState.message}`;
    ui.statusToggle.dataset.tone = state.syncState.tone;
    ui.statusToggle.setAttribute('aria-haspopup', 'dialog');
    ui.statusToggle.setAttribute('aria-expanded', state.showStatus ? 'true' : 'false');
    ui.statusToggle.setAttribute('aria-label', statusLabel);
    ui.statusToggle.title = statusLabel;
  }

  if (!ui.statusRoot) {
    return;
  }

  if (!state.showStatus) {
    ui.statusRoot.hidden = true;
    ui.statusRoot.innerHTML = '';
    return;
  }

  const syncMetadata = getSyncMetadata();
  const lastSyncText = syncMetadata.lastSyncAt ? formatDateTime(syncMetadata.lastSyncAt) : 'Never synced';
  const lastErrorText = syncMetadata.lastErrorSummary || 'None';

  ui.statusRoot.hidden = false;
  ui.statusRoot.innerHTML = `
    <div class="modal-layer status-modal-layer">
      <button type="button" id="status-backdrop" class="modal-backdrop" aria-label="Close sync status"></button>
      <section class="modal-card status-modal-card" role="dialog" aria-modal="true" aria-labelledby="status-modal-title">
        <div class="modal-header">
          <div>
            <p class="eyebrow">Sync</p>
            <h2 id="status-modal-title">Sync status</h2>
            <p class="card-copy">Review current sync state, diagnostics, and retry GitHub sync when the app reports a recoverable error.</p>
          </div>
          <button type="button" id="close-status" class="ghost-button compact-button" data-modal-autofocus="true">Close</button>
        </div>

        <section
          class="card status-summary-card ${state.syncState.kind === 'conflict' ? 'clickable-card' : ''}"
          ${state.syncState.kind === 'conflict'
            ? 'role="button" tabindex="0" data-action="open-conflict-modal" data-focus-key="status-summary-card" aria-label="Open conflict resolution"'
            : ''}
        >
          <p class="sync-status" data-tone="${escapeHtml(state.syncState.tone)}">${escapeHtml(state.syncState.message)}</p>
          <p class="status-detail">${escapeHtml(state.syncState.detail || describeRepoSettings(state.repoSettings))}</p>
          ${state.syncState.kind === 'conflict'
            ? '<p class="card-copy">Tap to review and resolve sync conflicts.</p>'
            : ''}
        </section>

        <section class="diagnostics-grid" aria-label="Status diagnostics">
          <div>
            <h3>Diagnostics</h3>
            <dl>
              <dt>State</dt>
              <dd>${escapeHtml(state.syncState.kind)}</dd>
              <dt>Last sync time</dt>
              <dd>${escapeHtml(lastSyncText)}</dd>
              <dt>Last error</dt>
              <dd>${escapeHtml(lastErrorText)}</dd>
            </dl>
          </div>
          <div class="security-panel">
            <h3>Repository</h3>
            <p>${escapeHtml(describeRepoSettings(state.repoSettings))}</p>
          </div>
        </section>

        <div class="form-actions status-actions">
          <button type="button" id="close-status-secondary" class="secondary-button">Close</button>
          <button type="button" id="retry-sync-modal" ${state.syncState.canRetry ? '' : 'disabled'}>Retry sync</button>
        </div>
      </section>
    </div>
  `;

  const closeStatus = () => {
    state.pendingFocusRequest = {
      type: 'focusKey',
      value: 'status-toggle',
    };
    state.showStatus = false;
    render();
  };

  ui.statusRoot.querySelector('#status-backdrop')?.addEventListener('click', closeStatus);
  ui.statusRoot.querySelector('#close-status')?.addEventListener('click', closeStatus);
  ui.statusRoot.querySelector('#close-status-secondary')?.addEventListener('click', closeStatus);
  ui.statusRoot.querySelector('#retry-sync-modal')?.addEventListener('click', () => {
    void handleRetryButtonClick();
  });
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
        <div class="nav-tabs" data-active-view="${state.currentView === 'tasks' ? 'tasks' : 'maps'}">
          <button
            type="button"
            class="nav-tab ${state.currentView === 'tasks' ? '' : 'active'}"
            data-action="switch-view"
            data-view="maps"
          >
            <span class="nav-tab-title">Maps</span>
          </button>
          <button
            type="button"
            class="nav-tab ${state.currentView === 'tasks' ? 'active' : ''}"
            data-action="switch-view"
            data-view="tasks"
          >
            <span class="nav-tab-title">Tasks</span>
          </button>
        </div>
      </nav>
      ${viewMarkup}
    </section>
    ${state.activeModal?.kind === 'resolveConflict' ? renderResolveConflictModal() : ''}
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
    fabSide: state.fabSide,
    hasToken: Boolean(getToken(state.repoSettings.tokenStorageKey)),
    syncMetadata: getSyncMetadata(),
    onSaveSettings: async (nextSettings) => {
      const normalizedSettings = normalizeRepoSettings({
        ...nextSettings,
        tokenStorageKey: state.runtimeConfig?.auth?.tokenStorageKey || state.repoSettings.tokenStorageKey,
      });
      saveRepoSettings(normalizedSettings, state.runtimeConfig?.auth?.settingsStorageKey);
      switchRepoContext(normalizedSettings);
      const nextFabSide = nextSettings.fabSide === 'left' ? 'left' : 'right';
      saveFabSidePreference(nextFabSide);
      state.fabSide = nextFabSide;
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
      state.pendingFocusRequest = {
        type: 'focusKey',
        value: 'settings-toggle',
      };
      state.showSettings = false;
      render();
    },
  });
}

function renderMapsView(summaries) {
  return `
    <section class="workspace-panel ${summaries.length === 0 ? 'empty-panel' : ''}">
      <div class="map-reader-header">
        <div class="compact-row-actions">
          <button
            type="button"
            class="secondary-button compact-button"
            data-action="open-create-map-modal"
            data-focus-key="create-map-trigger"
          >New map</button>
        </div>
      </div>
      ${summaries.length === 0
        ? `
          <article class="card empty-card">
            <p>The configured FocusMaps folder is reachable, but no top-level .json map files were found.</p>
            <p class="section-copy">Point <code>repoPath</code> directly at the FocusMaps folder, for example <code>Tool/PMMT/FocusMaps</code>, and make sure the folder contains map JSON files.</p>
          </article>
        `
        : `
          <div class="compact-list map-list">
            ${summaries.map((summary) => renderMapCard(summary)).join('')}
          </div>
        `}
      ${renderActiveModal()}
    </section>
  `;
}

function renderMapCard(summary) {
  return `
    <article
      class="card compact-row map-row clickable-card"
      role="button"
      tabindex="0"
      data-action="open-map"
      data-map-path="${escapeHtml(summary.filePath)}"
      aria-label="Open map ${escapeHtml(summary.fileName)}"
      title="Open ${escapeHtml(summary.fileName)}"
    >
      <div class="compact-row-main">
        <div class="compact-title-block">
          <h3>${renderInlineHtml(summary.rootTitle, { theme: state.theme, wrapperClass: 'formatted-inline card-title-inline' })}</h3>
          <p class="map-file-name">${escapeHtml(summary.fileName)}</p>
          <p class="compact-meta">${renderMapTaskCounts(summary)}</p>
        </div>
      </div>
      <div class="compact-row-actions">
        <p class="map-updated">Updated ${escapeHtml(formatRelativeTime(summary.updatedAt))}</p>
      </div>
      <div class="card-menu">
        <button
          type="button"
          class="ghost-button compact-button card-menu-trigger"
          data-action="toggle-card-menu"
          data-map-path="${escapeHtml(summary.filePath)}"
          aria-label="Map options"
          aria-expanded="${state.openCardMenu === summary.filePath}"
        >&#x22EE;</button>
        ${state.openCardMenu === summary.filePath ? `
          <div class="card-menu-dropdown" role="menu">
            <button
              type="button"
              class="card-menu-item mini-action--destructive"
              role="menuitem"
              data-action="open-delete-map-modal"
              data-map-path="${escapeHtml(summary.filePath)}"
            >Delete</button>
          </div>
        ` : ''}
      </div>
    </article>
  `;
}

function renderMapTaskCounts(summary) {
  const { open, todo, doing, done } = summary.taskCounts;
  const parts = [
    `<span class="task-tone-open">${open}</span>`,
    `<span class="task-tone-todo">${todo}</span>`,
    `<span class="task-tone-doing">${doing}</span>`,
    `<span class="task-tone-done">${done}</span>`,
  ];
  const pendingCount = getPendingCountForMap(summary.filePath);
  if (pendingCount > 0) {
    parts.push(`Pending ${pendingCount}`);
  }
  return parts.join(' · ');
}

function renderMapView() {
  const snapshot = getSelectedSnapshot();
  if (!snapshot) {
    state.currentView = 'maps';
    if (window.location.hash !== HASH_ROUTE.maps) {
      replaceHashRoute(HASH_ROUTE.maps);
    }
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

  const rootNode = snapshot.document.rootNode;
  const rootHasChildren = Array.isArray(rootNode?.children) && rootNode.children.length > 0;
  const rootCollapsed = getLocalCollapsedState(snapshot.filePath, rootNode);
  const isRootSelected = selectedNodeState.node.uniqueIdentifier === rootNode.uniqueIdentifier;

  return `
    <section class="workspace-panel map-reader-view">
      <article class="map-document" aria-label="Map reader">
        <header class="map-document-header ${isRootSelected ? 'selected' : ''}">
          <div class="map-document-line">
            ${rootHasChildren
              ? `<button
                  type="button"
                  class="tree-toggle compact-toggle"
                  data-action="toggle-node"
                  data-map-path="${escapeHtml(snapshot.filePath)}"
                  data-node-id="${escapeHtml(rootNode.uniqueIdentifier)}"
                  aria-label="${rootCollapsed ? 'Expand' : 'Collapse'} node"
                >${rootCollapsed ? '+' : '-'}</button>`
              : '<span class="tree-spacer root-spacer" aria-hidden="true"></span>'}
            ${renderPassiveTaskDot(rootNode.taskState, 'root-task-dot')}
            <div class="map-document-body">
              <div
                role="button"
                tabindex="0"
                class="map-document-title ${isRootSelected ? 'selected' : ''}"
                data-action="select-node"
                data-map-path="${escapeHtml(snapshot.filePath)}"
                data-node-id="${escapeHtml(rootNode.uniqueIdentifier)}"
                data-focus-key="${escapeHtml(buildNodeFocusKey(snapshot.filePath, rootNode.uniqueIdentifier))}"
              >
                ${renderNodeTextMarkup(rootNode.name, 'formatted-inline map-title-inline')}
              </div>
              ${isRootSelected ? renderSelectedNodeActions(snapshot, selectedNodeState) : ''}
            </div>
          </div>
        </header>

        ${rootHasChildren && !rootCollapsed
          ? `<ol class="reader-list reader-root-list">${rootNode.children.map((child) => renderTreeNode(snapshot, child, 1, selectedNodeState)).join('')}</ol>`
          : ''}
      </article>

      ${renderActiveModal()}

      <button
        type="button"
        class="add-note-fab add-note-fab--${state.fabSide}"
        data-action="open-modal"
        data-modal-kind="addChildNote"
        data-map-path="${escapeHtml(snapshot.filePath)}"
        data-node-id="${escapeHtml(selectedNodeState.node.uniqueIdentifier)}"
        data-focus-key="add-note-fab"
        aria-label="Add note"
        title="Add note"
        ${selectedNodeState.canEditNode ? '' : 'disabled'}
      >
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <line x1="12" y1="5" x2="12" y2="19"/>
          <line x1="5" y1="12" x2="19" y2="12"/>
        </svg>
      </button>
    </section>
  `;
}

function renderTreeNode(snapshot, node, depth, selectedNodeState) {
  const isSelected =
    state.selectedMapPath === snapshot.filePath &&
    state.selectedNodeId === node.uniqueIdentifier;
  const hasChildren = Array.isArray(node.children) && node.children.length > 0;
  const isCollapsed = getLocalCollapsedState(snapshot.filePath, node);
  const nodeTextMarkup = renderNodeTextMarkup(node.name, 'formatted-inline tree-inline');

  return `
    <li class="reader-node ${isSelected ? 'selected' : ''}" style="--depth:${depth}">
      <div class="reader-node-line">
        ${hasChildren
          ? `<button
              type="button"
              class="tree-toggle compact-toggle"
              data-action="toggle-node"
              data-map-path="${escapeHtml(snapshot.filePath)}"
              data-node-id="${escapeHtml(node.uniqueIdentifier)}"
              aria-label="${isCollapsed ? 'Expand' : 'Collapse'} node"
            >${isCollapsed ? '+' : '-'}</button>`
          : '<span class="tree-spacer" aria-hidden="true"></span>'}
        ${renderNodeBullet(snapshot.filePath, node)}
        <div class="reader-node-body">
          <div
            role="button"
            tabindex="0"
            class="tree-label ${isSelected ? 'selected' : ''}"
            data-action="select-node"
            data-map-path="${escapeHtml(snapshot.filePath)}"
            data-node-id="${escapeHtml(node.uniqueIdentifier)}"
            data-focus-key="${escapeHtml(buildNodeFocusKey(snapshot.filePath, node.uniqueIdentifier))}"
          >
            ${nodeTextMarkup}
          </div>
          ${isSelected ? renderSelectedNodeActions(snapshot, selectedNodeState) : ''}
        </div>
      </div>
      ${hasChildren && !isCollapsed
        ? `<ul class="reader-list child-list">${node.children.map((child) => renderTreeNode(snapshot, child, depth + 1, selectedNodeState)).join('')}</ul>`
        : ''}
    </li>
  `;
}

function renderSelectedNodeActions(snapshot, nodeUiState) {
  const nodeId = nodeUiState.node.uniqueIdentifier;
  const mapPath = snapshot.filePath;
  const taskDisabled = nodeUiState.canChangeTaskState ? '' : 'disabled';
  const isTask = nodeUiState.node.taskState !== TASK_STATE.NONE;
  const badgesMarkup = nodeUiState.badges.length > 0
    ? `<div class="selected-node-meta">${renderBadgeMarkup(nodeUiState.badges)}</div>`
    : '';
  const hintMarkup = renderSelectedNodeHint(nodeUiState);

  if (!isTask && !badgesMarkup && !hintMarkup) {
    return '';
  }

  return `
    <div class="selected-node-actions">
      <div class="selected-node-toolbar">
        ${isTask ? `
          <div class="task-dot-group" role="group" aria-label="Task state">
            ${renderTaskStateDotButton('Clear task state', TASK_STATE.NONE, nodeUiState, taskDisabled)}
            ${renderTaskStateDotButton('Set Todo', TASK_STATE.TODO, nodeUiState, taskDisabled)}
            ${renderTaskStateDotButton('Set Doing', TASK_STATE.DOING, nodeUiState, taskDisabled)}
            ${renderTaskStateDotButton('Set Done', TASK_STATE.DONE, nodeUiState, taskDisabled)}
          </div>
        ` : ''}
      </div>
      ${badgesMarkup}
      ${hintMarkup}
    </div>
  `;
}

function renderSelectedNodeHint(nodeUiState) {
  if (!nodeUiState.canEditNode) {
    return '<p class="selected-node-hint">Idea tag is preserved but read-only in the PWA.</p>';
  }

  if (!nodeUiState.canChangeTaskState) {
    return '';
  }

  return '';
}

function renderTaskStateDotButton(label, taskState, nodeUiState, disabledAttribute) {
  const isActive = nodeUiState.node.taskState === taskState;
  return `
    <button
      type="button"
      class="task-dot-button ${taskStateToneClass(taskState)} ${isActive ? 'active' : ''}"
      data-action="set-task-state"
      data-task-state="${taskState}"
      aria-label="${escapeHtml(label)}"
      title="${escapeHtml(label)}"
      ${disabledAttribute}
    >
      <span class="sr-only">${escapeHtml(label)}</span>
    </button>
  `;
}

function renderNodeBullet(mapPath, node) {
  if (node.taskState === TASK_STATE.NONE) {
    return `
      <button
        type="button"
        class="task-dot task-dot-clickable is-none"
        data-action="set-task-state"
        data-task-state="${TASK_STATE.TODO}"
        data-map-path="${escapeHtml(mapPath)}"
        data-node-id="${escapeHtml(node.uniqueIdentifier)}"
        aria-label="Mark as Todo"
        title="Mark as Todo"
      ></button>
    `;
  }
  return renderPassiveTaskDot(node.taskState);
}

function renderPassiveTaskDot(taskState, extraClass = '') {
  return `<span class="task-dot ${taskStateToneClass(taskState)} ${escapeHtml(extraClass)}" aria-hidden="true"></span>`;
}

function taskStateToneClass(taskState) {
  switch (taskState) {
    case TASK_STATE.TODO:
      return 'is-todo';
    case TASK_STATE.DOING:
      return 'is-doing';
    case TASK_STATE.DONE:
      return 'is-done';
    default:
      return 'is-none';
  }
}

function buildModalTriggerKey(kind, mapPath, nodeId) {
  return `modal|${kind}|${mapPath}|${nodeId}`;
}

function renderActiveModal() {
  if (!state.activeModal) {
    return '';
  }

  if (state.activeModal.kind === 'createMap') {
    return renderCreateMapModal();
  }

  if (state.activeModal.kind === 'deleteMap') {
    return renderDeleteMapModal();
  }

  const modalContext = getActiveModalContext(state.activeModal.kind);
  if (!modalContext) {
    return '';
  }

  const { nodeUiState } = modalContext;

  if (state.activeModal.kind === 'deleteNode') {
    return renderDeleteNodeModal(nodeUiState);
  }

  const isEditModal = state.activeModal.kind === 'editNode';
  const isAddTaskModal = state.activeModal.kind === 'addChildTask';
  const title = isEditModal
    ? 'Edit node text'
    : isAddTaskModal
      ? 'Add child task'
      : 'Add child note';
  const description = isEditModal
    ? 'Unsupported mind-map data stays preserved when you save supported edits.'
    : isAddTaskModal
      ? 'Create a new child task under the selected node. New tasks start in Todo.'
      : 'Create a new child note under the selected node.';
  const formId = isEditModal
    ? 'edit-node-form'
    : isAddTaskModal
      ? 'add-task-form'
      : 'add-note-form';
  const actionLabel = isEditModal
    ? 'Save'
    : isAddTaskModal
      ? 'Add task'
      : 'Add note';
  const inputMarkup = isEditModal
    ? `
      <label>
        <span>Node text</span>
        <textarea
          id="edit-node-text"
          name="text"
          rows="5"
          maxlength="5000"
          data-modal-autofocus="true"
        >${escapeHtml(state.activeModal.draftText)}</textarea>
      </label>
      <div class="preview-block">
        <p class="preview-label">Rendered preview</p>
        <div class="preview-panel" data-inline-preview="edit-node">
          ${renderNodePreviewMarkup(state.activeModal.draftText, nodeUiState.node.taskState)}
        </div>
      </div>
    `
    : `
      <label>
        <span>${isAddTaskModal ? 'Child task text' : 'Child note text'}</span>
        <input
          type="text"
          name="text"
          maxlength="500"
          value="${escapeHtml(state.activeModal.draftText)}"
          placeholder="${escapeHtml(isAddTaskModal ? 'Describe the new task' : 'Describe the new child note')}"
          data-modal-autofocus="true"
        />
      </label>
    `;

  return `
    <div class="modal-layer">
      <button type="button" class="modal-backdrop" data-action="close-modal" aria-label="Close dialog"></button>
      <div class="modal-card" role="dialog" aria-modal="true" aria-labelledby="map-modal-title">
        ${isEditModal ? '' : `
        <div class="modal-header">
          <div>
            <p class="eyebrow">Add child</p>
            <h3 id="map-modal-title">${escapeHtml(title)}</h3>
            <p class="node-path">${renderInlinePath(nodeUiState.pathSegments, 'formatted-path node-path-inline')}</p>
          </div>
          <button type="button" class="ghost-button compact-button" data-action="close-modal">Close</button>
        </div>
        `}

        ${state.activeModal.errorMessage
          ? `<p class="form-error" role="alert">${escapeHtml(state.activeModal.errorMessage)}</p>`
          : ''}

        <form id="${formId}" class="stack-form modal-form">
          ${inputMarkup}
          ${isEditModal ? '' : `<p class="security-note">${escapeHtml(description)}</p>`}
          <div class="form-actions">
            <button type="button" class="secondary-button" data-action="close-modal">Cancel</button>
            ${isEditModal && nodeUiState.canChangeTaskState ? `
            <button
              type="button"
              class="danger-button"
              data-action="open-modal"
              data-modal-kind="deleteNode"
              data-map-path="${escapeHtml(state.activeModal.mapPath)}"
              data-node-id="${escapeHtml(nodeUiState.node.uniqueIdentifier)}"
              data-focus-key="${escapeHtml(buildModalTriggerKey('deleteNode', state.activeModal.mapPath, nodeUiState.node.uniqueIdentifier))}"
            >Delete</button>
            ` : ''}
            <button type="submit">${escapeHtml(actionLabel)}</button>
          </div>
        </form>
      </div>
    </div>
  `;
}

function renderCreateMapModal() {
  return `
    <div class="modal-layer">
      <button type="button" class="modal-backdrop" data-action="close-modal" aria-label="Close dialog"></button>
      <div class="modal-card" role="dialog" aria-modal="true" aria-labelledby="map-modal-title">
        <div class="modal-header">
          <div>
            <p class="eyebrow">Maps</p>
            <h3 id="map-modal-title">New map</h3>
          </div>
          <button type="button" class="ghost-button compact-button" data-action="close-modal">Close</button>
        </div>

        ${state.activeModal.errorMessage
          ? `<p class="form-error" role="alert">${escapeHtml(state.activeModal.errorMessage)}</p>`
          : ''}

        <form id="create-map-form" class="stack-form modal-form">
          <label>
            <span>Map name</span>
            <input
              type="text"
              name="mapName"
              maxlength="200"
              value="${escapeHtml(state.activeModal.draftText)}"
              placeholder="Enter a map name"
              data-modal-autofocus="true"
            />
          </label>
          <p class="security-note">The map is saved as &lt;name&gt;.json in the configured FocusMaps folder.</p>
          <div class="form-actions">
            <button type="button" class="secondary-button" data-action="close-modal">Cancel</button>
            <button type="submit">Create</button>
          </div>
        </form>
      </div>
    </div>
  `;
}

function renderDeleteNodeModal(nodeUiState) {
  const node = nodeUiState.node;
  const isTask = node.taskState !== TASK_STATE.NONE;
  const nodeKind = isTask ? 'task' : 'note';
  const childCount = Array.isArray(node.children) ? node.children.length : 0;
  const childWarning = childCount > 0
    ? `<p class="form-error" role="note">This ${nodeKind} has ${childCount} child node${childCount === 1 ? '' : 's'} that will also be removed.</p>`
    : '';

  return `
    <div class="modal-layer">
      <button type="button" class="modal-backdrop" data-action="close-modal" aria-label="Close dialog"></button>
      <div class="modal-card" role="dialog" aria-modal="true" aria-labelledby="map-modal-title">
        <div class="modal-header">
          <div>
            <p class="eyebrow">Remove ${escapeHtml(nodeKind)}</p>
            <h3 id="map-modal-title">Confirm removal</h3>
            <p class="node-path">${renderInlinePath(nodeUiState.pathSegments, 'formatted-path node-path-inline')}</p>
          </div>
          <button type="button" class="ghost-button compact-button" data-action="close-modal">Close</button>
        </div>

        ${state.activeModal.errorMessage
          ? `<p class="form-error" role="alert">${escapeHtml(state.activeModal.errorMessage)}</p>`
          : ''}

        ${childWarning}

        <p class="security-note">Remove this ${escapeHtml(nodeKind)} from the map? This cannot be undone.</p>
        <div class="form-actions">
          <button type="button" class="secondary-button" data-action="close-modal" data-modal-autofocus="true">Cancel</button>
          <button
            type="button"
            class="danger-button"
            data-action="confirm-delete-node"
          >Remove</button>
        </div>
      </div>
    </div>
  `;
}

function renderDeleteMapModal() {
  const filePath = state.activeModal.mapPath;
  const snapshot = state.mapsByPath[filePath];
  const mapName = snapshot?.mapName || filePath;

  return `
    <div class="modal-layer">
      <button type="button" class="modal-backdrop" data-action="close-modal" aria-label="Close dialog"></button>
      <div class="modal-card" role="dialog" aria-modal="true" aria-labelledby="map-modal-title">
        <div class="modal-header">
          <div>
            <p class="eyebrow">Delete map</p>
            <h3 id="map-modal-title">${escapeHtml(mapName)}</h3>
          </div>
          <button type="button" class="ghost-button compact-button" data-action="close-modal">Close</button>
        </div>

        ${state.activeModal.errorMessage
          ? `<p class="form-error" role="alert">${escapeHtml(state.activeModal.errorMessage)}</p>`
          : ''}

        <p class="security-note">This will permanently delete the map file from GitHub. This cannot be undone.</p>
        <div class="form-actions">
          <button type="button" class="secondary-button" data-action="close-modal" data-modal-autofocus="true">Cancel</button>
          <button
            type="button"
            class="danger-button"
            data-action="confirm-delete-map"
          >Delete</button>
        </div>
      </div>
    </div>
  `;
}

function renderTasksView() {
  const entries = buildTaskEntriesForView(state.taskFilter);
  const filterButtons = [
    ['open', 'Open', 'open'],
    ['todo', 'Todo', 'todo'],
    ['doing', 'Doing', 'doing'],
    ['done', 'Done', 'done'],
  ];

  return `
    <section class="workspace-panel">
      <h2 class="sr-only">All task nodes</h2>
      <div class="filter-row">
        ${filterButtons.map(([value, label, tone]) => `
          <button
            type="button"
            class="filter-pill filter-pill--${tone} ${state.taskFilter === value ? 'active' : ''}"
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
          <div class="compact-list task-entry-list">
            ${entries.map((entry) => `
              <article
                class="card compact-row task-row clickable-card"
                role="button"
                tabindex="0"
                data-action="open-task-node"
                data-map-path="${escapeHtml(entry.filePath)}"
                data-node-id="${escapeHtml(entry.nodeId)}"
                aria-label="Open task ${escapeHtml(entry.nodeName)}"
                title="Open task ${escapeHtml(entry.nodeName)}"
              >
                <div class="compact-row-main task-row-main">
                  ${renderPassiveTaskDot(entry.taskState)}
                  <div class="compact-title-block">
                    <h3>${renderInlineHtml(entry.nodeName, { theme: state.theme, wrapperClass: 'formatted-inline task-title-inline' })}</h3>
                    <p class="task-entry-map">${escapeHtml(entry.mapName)}</p>
                    <p class="task-entry-path">${renderInlinePath(entry.nodePathSegments || entry.nodePath.split(' > '), 'formatted-path task-path-inline')}</p>
                  </div>
                </div>
              </article>
            `).join('')}
          </div>
        `}
    </section>
  `;
}

function renderThemeModeControl(extraClass = '') {
  const className = ['theme-switcher', extraClass].filter(Boolean).join(' ');
  return `
    <fieldset class="${className}" aria-label="Theme preference">
      <label class="theme-choice ${state.theme === 'light' ? 'active' : ''}">
        <input type="radio" name="theme-mode" value="light" ${state.theme === 'light' ? 'checked' : ''} />
        <span>Light</span>
      </label>
      <label class="theme-choice ${state.theme === 'dark' ? 'active' : ''}">
        <input type="radio" name="theme-mode" value="dark" ${state.theme === 'dark' ? 'checked' : ''} />
        <span>Dark</span>
      </label>
    </fieldset>
  `;
}

function renderNodeTextMarkup(rawText, wrapperClass) {
  return renderInlineHtml(normalizeNodeDisplayText(rawText), {
    theme: state.theme,
    wrapperClass,
  });
}

function renderNodePreviewMarkup(rawText, taskState) {
  return `
    <div class="preview-inline-row">
      ${renderPassiveTaskDot(taskState, 'preview-task-dot')}
      ${renderNodeTextMarkup(rawText, 'formatted-inline preview-inline')}
    </div>
  `;
}

function renderInlinePath(pathSegments, wrapperClass = 'formatted-path') {
  const segments = Array.isArray(pathSegments) ? pathSegments : [];
  if (segments.length === 0) {
    return renderInlineHtml(normalizeNodeDisplayText(''), {
      theme: state.theme,
      wrapperClass,
    });
  }

  return `
    <span class="${escapeHtml(wrapperClass)}">
      ${segments.map((segment) => renderInlineHtml(segment, {
        theme: state.theme,
        wrapperClass: 'formatted-inline path-segment-inline',
      })).join('<span class="path-separator"> > </span>')}
    </span>
  `;
}

function buildTaskEntriesForView(filter) {
  const effectiveFilter = filter || 'all';
  return getSnapshots()
    .flatMap((snapshot) => collectTaskEntries(snapshot, effectiveFilter))
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
    case 'deleteNode':
      return `node removal in ${mapLabel}`;
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
