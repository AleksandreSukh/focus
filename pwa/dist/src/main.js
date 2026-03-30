import {
  buildTodoAddCommitMessage,
  buildTodoDeleteCommitMessage,
  buildTodoEditCommitMessage,
  buildTodoToggleCommitMessage,
  getSyncMetadata,
  recordSyncState,
} from './gitProvider/index.js';
import { clearToken, getToken, saveToken, validateToken } from './auth/index.js';
import { renderTokenEntryScreen } from './auth/TokenEntryScreen.js';
import {
  buildRepoFilePath,
  buildRepoScope,
  describeRepoSettings,
  getEffectiveRepoSettings,
  isRepoSettingsComplete,
  normalizeRepoSettings,
  saveRepoSettings,
} from './settings/repoSettings.js';
import { renderConnectionScreen } from './settings/ConnectionScreen.js';
import { renderSettingsScreen } from './settings/SettingsScreen.js';
import { GitHubTodoProvider } from './todos/githubTodoProvider.js';
import {
  loadCachedTodos,
  loadPendingOperations,
  saveCachedTodos,
  savePendingOperations,
} from './todos/localCache.js';
import { TodoRepository } from './todos/todoRepository.js';
import { TodoService } from './todos/todoService.js';

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
  todos: [],
  pendingOperations: [],
  service: null,
  processingQueue: false,
  installEvent: null,
  showSettings: false,
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
  ui.addForm = document.getElementById('add-form');
  ui.addInput = document.getElementById('add-input');
  ui.taskList = document.getElementById('task-list');
  ui.installButton = document.getElementById('install-button');
  ui.installFallback = document.getElementById('install-fallback');
  ui.settingsToggle = document.getElementById('settings-toggle');
  ui.statusMessage = document.getElementById('sync-status');
  ui.statusDetail = document.getElementById('sync-detail');
  ui.retryButton = document.getElementById('retry-sync');
  ui.screenRoot = document.getElementById('screen-root');
  ui.tasksView = document.getElementById('tasks-view');
  ui.settingsRoot = document.getElementById('settings-root');
  bindGlobalUiListeners();

  ui.addForm?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const value = ui.addInput?.value.trim() ?? '';
    if (!value) {
      return;
    }

    const now = new Date().toISOString();
    const todoId = globalThis.crypto?.randomUUID?.() ?? `${now}-${Math.random().toString(16).slice(2)}`;
    state.todos = [
      {
        id: todoId,
        text: value,
        completed: false,
        deleted: false,
        createdAt: now,
        updatedAt: now,
      },
      ...state.todos,
    ];
    ui.addInput.value = '';

    enqueueOperation({
      type: 'add',
      todoId,
      text: value,
      createdAt: now,
      updatedAt: now,
      commitMessage: buildTodoAddCommitMessage(value),
    });
    render();
  });

  ui.taskList?.addEventListener('click', async (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }

    const item = target.closest('[data-task-id]');
    if (!(item instanceof HTMLElement)) {
      return;
    }

    const todoId = item.dataset.taskId;
    if (!todoId) {
      return;
    }

    if (target.matches('.toggle')) {
      const todo = state.todos.find((entry) => entry.id === todoId);
      if (!todo) {
        return;
      }

      const updatedAt = new Date().toISOString();
      const completed = !todo.completed;
      state.todos = state.todos.map((entry) =>
        entry.id === todoId ? { ...entry, completed, updatedAt } : entry,
      );
      enqueueOperation({
        type: 'setCompleted',
        todoId,
        completed,
        updatedAt,
        commitMessage: buildTodoToggleCommitMessage(todoId, completed),
      });
      render();
      return;
    }

    if (target.matches('.delete')) {
      const todo = state.todos.find((entry) => entry.id === todoId);
      if (!todo) {
        return;
      }

      const confirmed = window.confirm(`Delete "${todo.text}"?`);
      if (!confirmed) {
        return;
      }

      state.todos = state.todos.filter((entry) => entry.id !== todoId);
      enqueueOperation({
        type: 'delete',
        todoId,
        updatedAt: new Date().toISOString(),
        commitMessage: buildTodoDeleteCommitMessage(todoId),
      });
      render();
      return;
    }

    if (target.matches('.task-label')) {
      startInlineEdit(item, todoId);
    }
  });

  ui.settingsToggle?.addEventListener('click', () => {
    state.showSettings = !state.showSettings;
    render();
  });

  ui.retryButton?.addEventListener('click', async () => {
    state.syncState = {
      kind: 'loadingRemote',
      tone: 'pending',
      message: 'Retrying sync…',
      detail: buildStatusDetail(),
      canRetry: false,
    };
    render();
    await handleRetry();
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

  if (form.id === 'connection-form') {
    event.preventDefault();
    await handleConnectionFormSubmit(form);
    return;
  }

  if (form.id === 'token-entry-form') {
    event.preventDefault();
    await handleTokenFormSubmit(form);
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

  if (button.id === 'retry-sync' && button !== ui.retryButton) {
    event.preventDefault();
    void handleRetryButtonClick();
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
  }
}

async function handleRetryButtonClick() {
  state.syncState = {
    kind: 'loadingRemote',
    tone: 'pending',
    message: 'Retrying syncâ€¦',
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
  state.todos = loadCachedTodos(state.repoScope);
  state.pendingOperations = loadPendingOperations(state.repoScope);
  state.service = null;
}

async function authenticateAndLoad() {
  if (!isRepoSettingsComplete(state.repoSettings)) {
    state.authState = 'missingConfig';
    state.connectionError = 'Repository owner, repository name, and branch are required.';
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
    message: 'Validating GitHub access…',
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
    } else if (validation.error.code === 'UNAUTHORIZED' || validation.error.code === 'FORBIDDEN') {
      clearToken(state.repoSettings.tokenStorageKey);
      state.authState = 'authFailed';
      state.tokenError = validation.error.message;
    } else {
      state.authState = 'authFailed';
      state.tokenError = validation.error.message;
    }

    render();
    return;
  }

  state.service = createTodoService(state.repoSettings, token);
  recordSyncState('loadingRemote', 'Loading todos from GitHub.');
  state.syncState = {
    kind: 'loadingRemote',
    tone: 'pending',
    message: 'Loading tasks from GitHub…',
    detail: describeRepoSettings(state.repoSettings),
    canRetry: false,
  };
  render();

  const loaded = await state.service.list(true);
  if (!loaded.ok) {
    handleSyncFailure(loaded.error, 'Could not load todos from GitHub.');
    render();
    return;
  }

  state.authState = 'authenticated';
  state.todos = applyOperationsLocally(loaded.value, state.pendingOperations);
  persistRepoScopedState();
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

function createTodoService(repoSettings, token) {
  const provider = new GitHubTodoProvider({
    owner: repoSettings.repoOwner,
    repo: repoSettings.repoName,
    branch: repoSettings.repoBranch,
    token,
    filePath: buildRepoFilePath(repoSettings),
  });
  return new TodoService(new TodoRepository(provider));
}

function enqueueOperation(operation) {
  state.pendingOperations = [...state.pendingOperations, operation];
  persistRepoScopedState();
  state.syncState = {
    kind: 'syncing',
    tone: 'pending',
    message: `${state.pendingOperations.length} local change${state.pendingOperations.length === 1 ? '' : 's'} waiting to sync.`,
    detail: buildStatusDetail(),
    canRetry: false,
  };
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
      message: `Syncing ${describeOperation(currentOperation)}…`,
      detail: buildStatusDetail(),
      canRetry: false,
    };
    render();

    const result = await executeOperation(currentOperation);
    if (!result.ok) {
      state.processingQueue = false;
      handleSyncFailure(result.error, `Could not sync ${describeOperation(currentOperation)}.`);
      render();
      return;
    }

    state.pendingOperations = state.pendingOperations.slice(1);
    const currentList = await state.service.list();
    if (currentList.ok) {
      state.todos = applyOperationsLocally(currentList.value, state.pendingOperations);
    }

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

async function executeOperation(operation) {
  switch (operation.type) {
    case 'add':
      return state.service.add(operation.text, operation.commitMessage, operation.todoId);
    case 'edit':
      return state.service.edit(operation.todoId, operation.text, operation.commitMessage);
    case 'setCompleted':
      return state.service.setCompleted(operation.todoId, operation.completed, operation.commitMessage);
    case 'delete':
      return state.service.delete(operation.todoId, operation.commitMessage);
    default:
      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: `Unsupported operation "${operation.type}".`,
          retriable: false,
        },
      };
  }
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

function applyOperationsLocally(baseTodos, operations) {
  let nextTodos = Array.isArray(baseTodos) ? [...baseTodos] : [];

  operations.forEach((operation) => {
    switch (operation.type) {
      case 'add':
        if (!nextTodos.some((todo) => todo.id === operation.todoId)) {
          nextTodos = [
            {
              id: operation.todoId,
              text: operation.text,
              completed: false,
              deleted: false,
              createdAt: operation.createdAt,
              updatedAt: operation.updatedAt,
            },
            ...nextTodos,
          ];
        }
        break;
      case 'edit':
        nextTodos = nextTodos.map((todo) =>
          todo.id === operation.todoId
            ? { ...todo, text: operation.text, updatedAt: operation.updatedAt }
            : todo,
        );
        break;
      case 'setCompleted':
        nextTodos = nextTodos.map((todo) =>
          todo.id === operation.todoId
            ? { ...todo, completed: operation.completed, updatedAt: operation.updatedAt }
            : todo,
        );
        break;
      case 'delete':
        nextTodos = nextTodos.filter((todo) => todo.id !== operation.todoId);
        break;
      default:
        break;
    }
  });

  return nextTodos;
}

function persistRepoScopedState() {
  saveCachedTodos(state.repoScope, state.todos);
  savePendingOperations(state.repoScope, state.pendingOperations);
}

function buildStatusDetail() {
  const parts = [describeRepoSettings(state.repoSettings)];
  const metadata = getSyncMetadata();

  if (metadata.lastSyncAt) {
    parts.push(`Last sync ${new Date(metadata.lastSyncAt).toLocaleString()}`);
  }

  if (state.pendingOperations.length > 0) {
    parts.push(`${state.pendingOperations.length} pending change${state.pendingOperations.length === 1 ? '' : 's'}`);
  }

  return parts.filter(Boolean).join(' · ');
}

function buildErrorDetail(error) {
  const parts = [describeRepoSettings(state.repoSettings)];
  const source = error?.cause && typeof error.cause === 'object' ? error.cause : error;
  const contextLabel =
    typeof source?.contextLabel === 'string' && source.contextLabel
      ? source.contextLabel
      : typeof error?.contextLabel === 'string' && error.contextLabel
        ? error.contextLabel
        : '';
  const status =
    typeof source?.status === 'number'
      ? source.status
      : typeof error?.status === 'number'
        ? error.status
        : null;
  const code =
    typeof source?.code === 'string' && source.code !== 'PERSISTENCE_ERROR'
      ? source.code
      : typeof error?.code === 'string' && error.code !== 'PERSISTENCE_ERROR'
        ? error.code
        : '';

  if (contextLabel) {
    parts.push(`Step: ${contextLabel}`);
  }

  if (typeof status === 'number') {
    parts.push(`HTTP ${status}`);
  }

  if (code) {
    parts.push(`Code: ${code}`);
  }

  return parts.filter(Boolean).join(' | ');
}

function render() {
  renderStatus();
  renderInstallHint();
  renderAuthOrTasks();
}

function renderStatus() {
  if (ui.statusMessage) {
    ui.statusMessage.textContent = state.syncState.message;
    ui.statusMessage.dataset.tone = state.syncState.tone;
  }

  if (ui.statusDetail) {
    ui.statusDetail.textContent = state.syncState.detail || buildStatusDetail();
  }

  if (ui.retryButton) {
    ui.retryButton.hidden = !state.syncState.canRetry;
  }
}

function renderInstallHint() {
  updateInstallState();
}

function renderAuthOrTasks() {
  if (state.authState === 'authenticated') {
    ui.screenRoot.hidden = true;
    ui.tasksView.hidden = false;
    if (ui.settingsToggle) {
      ui.settingsToggle.hidden = false;
      ui.settingsToggle.textContent = state.showSettings ? 'Hide settings' : 'Connection settings';
    }
    renderTaskList();
    renderSettingsPanel();
    return;
  }

  ui.tasksView.hidden = true;
  ui.screenRoot.hidden = false;
  ui.settingsRoot.hidden = true;

  if (ui.settingsToggle) {
    ui.settingsToggle.hidden = true;
  }

  if (state.authState === 'missingConfig') {
    renderConnectionScreen({
      mountNode: ui.screenRoot,
      initialValues: state.repoSettings,
      errorMessage: state.connectionError,
    });
    return;
  }

  renderTokenEntryScreen({
    mountNode: ui.screenRoot,
    repoLabel: describeRepoSettings(state.repoSettings),
    errorMessage: state.tokenError,
  });
}

function renderTaskList() {
  if (!ui.taskList) {
    return;
  }

  ui.taskList.textContent = '';
  const sortedTodos = [...state.todos].sort((left, right) => {
    const delta = Date.parse(right.updatedAt) - Date.parse(left.updatedAt);
    if (delta !== 0) {
      return delta;
    }

    return left.text.localeCompare(right.text);
  });

  if (sortedTodos.length === 0) {
    const empty = document.createElement('li');
    empty.className = 'task-item empty-state';
    empty.textContent = state.pendingOperations.length > 0
      ? 'No synced tasks yet. Pending local changes will appear after sync.'
      : 'No tasks yet. Add one above.';
    ui.taskList.append(empty);
    return;
  }

  sortedTodos.forEach((task) => {
    const item = document.createElement('li');
    item.className = 'task-item';
    item.dataset.taskId = task.id;

    const toggle = document.createElement('input');
    toggle.type = 'checkbox';
    toggle.className = 'toggle';
    toggle.checked = task.completed;
    toggle.setAttribute('aria-label', `Mark ${task.text} as ${task.completed ? 'open' : 'done'}`);

    const label = document.createElement('button');
    label.type = 'button';
    label.className = `task-label ${task.completed ? 'done' : ''}`;
    label.textContent = task.text;

    const meta = document.createElement('span');
    meta.className = 'task-meta';
    meta.textContent = formatTaskTimestamp(task.updatedAt);

    const remove = document.createElement('button');
    remove.type = 'button';
    remove.className = 'delete';
    remove.textContent = 'Delete';

    const body = document.createElement('div');
    body.className = 'task-body';
    body.append(label, meta);

    item.append(toggle, body, remove);
    ui.taskList.append(item);
  });
}

function renderSettingsPanel() {
  if (!ui.settingsRoot) {
    return;
  }

  if (!state.showSettings) {
    ui.settingsRoot.hidden = true;
    ui.settingsRoot.textContent = '';
    return;
  }

  ui.settingsRoot.hidden = false;
  renderSettingsScreen({
    mountNode: ui.settingsRoot,
    repoSettings: state.repoSettings,
    hasToken: Boolean(getToken(state.repoSettings.tokenStorageKey)),
    syncMetadata: getSyncMetadata(),
    onSaveSettings: handleSettingsSave,
    onClearToken: handleClearToken,
    onRevalidate: handleRetry,
    onClose: () => {
      state.showSettings = false;
      render();
    },
  });
}

async function handleConnectionSubmit(payload) {
  const nextSettings = normalizeRepoSettings(payload);
  saveRepoSettings(nextSettings, state.runtimeConfig.auth?.settingsStorageKey);
  saveToken(payload.token, nextSettings.tokenStorageKey);
  switchRepoContext(nextSettings);
  await authenticateAndLoad();
}

async function handleTokenSubmit(token) {
  saveToken(token, state.repoSettings.tokenStorageKey);
  await authenticateAndLoad();
}

async function handleConnectionFormSubmit(form) {
  const errorNode = form.parentElement?.querySelector('#connection-error');

  try {
    const formData = new FormData(form);
    const payload = {
      repoOwner: String(formData.get('repoOwner') ?? '').trim(),
      repoName: String(formData.get('repoName') ?? '').trim(),
      repoBranch: String(formData.get('repoBranch') ?? '').trim(),
      repoPath: String(formData.get('repoPath') ?? '').trim() || '/',
      token: String(formData.get('token') ?? '').trim(),
    };

    if (!payload.repoOwner || !payload.repoName || !payload.repoBranch || !payload.token) {
      if (errorNode instanceof HTMLElement) {
        errorNode.hidden = false;
        errorNode.textContent = 'Repository owner, repository name, branch, and token are required.';
      }
      return;
    }

    if (errorNode instanceof HTMLElement) {
      errorNode.hidden = true;
      errorNode.textContent = '';
    }

    await handleConnectionSubmit(payload);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    state.connectionError = message;
    state.syncState = {
      kind: 'error',
      tone: 'error',
      message: 'Connection setup failed before sync could start.',
      detail: message,
      canRetry: true,
    };
    render();
  }
}

async function handleTokenFormSubmit(form) {
  const errorNode = form.parentElement?.querySelector('#token-error');

  try {
    const tokenInput = form.querySelector('#token-input');
    const token = tokenInput instanceof HTMLInputElement ? tokenInput.value.trim() : '';
    if (!token) {
      if (errorNode instanceof HTMLElement) {
        errorNode.hidden = false;
        errorNode.textContent = 'Token is required.';
      }
      return;
    }

    if (errorNode instanceof HTMLElement) {
      errorNode.hidden = true;
      errorNode.textContent = '';
    }

    await handleTokenSubmit(token);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    state.tokenError = message;
    state.syncState = {
      kind: 'error',
      tone: 'error',
      message: 'Token validation failed before sync could start.',
      detail: message,
      canRetry: true,
    };
    render();
  }
}

async function handleSettingsSave(nextSettings) {
  const normalized = normalizeRepoSettings({
    ...state.repoSettings,
    ...nextSettings,
  });
  saveRepoSettings(normalized, state.runtimeConfig.auth?.settingsStorageKey);
  switchRepoContext(normalized);

  const token = getToken(normalized.tokenStorageKey);
  if (!token) {
    state.authState = 'needsToken';
    state.showSettings = false;
    state.syncState = {
      kind: 'idle',
      tone: 'warning',
      message: 'Settings saved. Enter a token to validate the new connection.',
      detail: describeRepoSettings(state.repoSettings),
      canRetry: false,
    };
    render();
    return;
  }

  await authenticateAndLoad();
}

function handleClearToken() {
  clearToken(state.repoSettings.tokenStorageKey);
  state.authState = 'needsToken';
  state.showSettings = false;
  state.tokenError = '';
  state.service = null;
  state.syncState = {
    kind: 'idle',
    tone: 'warning',
    message: 'Saved token cleared. Enter a new PAT to continue syncing.',
    detail: describeRepoSettings(state.repoSettings),
    canRetry: false,
  };
  render();
}

async function handleRetry() {
  if (state.authState !== 'authenticated') {
    await authenticateAndLoad();
    return;
  }

  if (state.pendingOperations.length > 0) {
    await processPendingOperations();
    return;
  }

  await authenticateAndLoad();
}

function startInlineEdit(item, todoId) {
  const currentTask = state.todos.find((todo) => todo.id === todoId);
  if (!currentTask) {
    return;
  }

  const input = document.createElement('input');
  input.className = 'edit-input';
  input.value = currentTask.text;
  input.setAttribute('aria-label', 'Edit task text');

  const commit = () => {
    const nextText = input.value.trim();
    if (!nextText || nextText === currentTask.text) {
      render();
      return;
    }

    const updatedAt = new Date().toISOString();
    state.todos = state.todos.map((todo) =>
      todo.id === todoId ? { ...todo, text: nextText, updatedAt } : todo,
    );
    enqueueOperation({
      type: 'edit',
      todoId,
      text: nextText,
      updatedAt,
      commitMessage: buildTodoEditCommitMessage(todoId),
    });
    render();
  };

  input.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      commit();
    }

    if (event.key === 'Escape') {
      render();
    }
  });

  input.addEventListener('blur', commit, { once: true });

  const body = item.querySelector('.task-body');
  if (body) {
    body.replaceChildren(input);
    input.focus();
    input.select();
  }
}

function describeOperation(operation) {
  switch (operation.type) {
    case 'add':
      return 'new task';
    case 'edit':
      return 'task edit';
    case 'setCompleted':
      return operation.completed ? 'task completion' : 'task reopen';
    case 'delete':
      return 'task deletion';
    default:
      return 'task change';
  }
}

function formatTaskTimestamp(value) {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : `Updated ${parsed.toLocaleString()}`;
}
