const runtimeConfig = window.__FOCUS_RUNTIME_CONFIG__ ?? {
  host: 'github-pages',
  repoOwner: '',
  repoName: '',
  repoBranch: 'main',
  repoPath: '/',
  auth: {
    tokenStorageKey: 'focus_runtime_token',
    tokenSource: 'runtime-only',
  },
};

window.focusRuntimeConfig = runtimeConfig;

const STORAGE_KEY = 'focus-pwa-tasks-v1';
const CURRENT_VERSION = 1;

const state = {
  tasks: readLocalTasks(),
  installEvent: null,
  syncInFlight: Promise.resolve(),
  remoteSha: null,
  git: resolveGitConfig(runtimeConfig),
};

const taskList = document.getElementById('task-list');
const addForm = document.getElementById('add-form');
const addInput = document.getElementById('add-input');
const installButton = document.getElementById('install-button');
const installFallback = document.getElementById('install-fallback');

const appHeader = document.querySelector('.app-header');
const statusBar = document.createElement('p');
statusBar.id = 'sync-status';
statusBar.className = 'sync-status';
statusBar.textContent = 'Authentication required.';

const authSection = document.createElement('section');
authSection.className = 'auth-panel';
authSection.innerHTML = `
  <h2>Connect to GitHub</h2>
  <p>Enter a GitHub Personal Access Token with repository contents access.</p>
  <form id="auth-form" class="auth-form">
    <label for="auth-token" class="sr-only">GitHub token</label>
    <input id="auth-token" type="password" autocomplete="off" placeholder="ghp_..." required />
    <button type="submit">Connect</button>
  </form>
  <p id="auth-error" role="alert" class="auth-error" hidden></p>
`;

const sessionControls = document.createElement('div');
sessionControls.className = 'session-controls';
sessionControls.innerHTML = '<button type="button" id="sign-out" class="install-button" hidden>Sign out</button>';

appHeader.insertAdjacentElement('afterend', statusBar);
statusBar.insertAdjacentElement('afterend', authSection);
appHeader.append(sessionControls);

const authForm = authSection.querySelector('#auth-form');
const authTokenInput = authSection.querySelector('#auth-token');
const authError = authSection.querySelector('#auth-error');
const signOutButton = sessionControls.querySelector('#sign-out');

addForm.addEventListener('submit', async (event) => {
  event.preventDefault();
  const value = addInput.value.trim();
  if (!value) return;

  const now = new Date().toISOString();
  state.tasks.unshift({
    id: crypto.randomUUID(),
    text: value,
    completed: false,
    createdAt: now,
    updatedAt: now,
  });

  addInput.value = '';
  await persistAndRender(buildCommitMessage('add', value));
  addInput.focus();
});

taskList.addEventListener('click', async (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;

  const item = target.closest('[data-task-id]');
  if (!(item instanceof HTMLElement)) return;
  const taskId = item.dataset.taskId;
  if (!taskId) return;

  if (target.matches('.toggle')) {
    await toggleTask(taskId);
    return;
  }

  if (target.matches('.delete')) {
    await deleteTask(taskId);
    return;
  }

  if (target.matches('.task-label')) {
    startInlineEdit(item, taskId);
  }
});

authForm?.addEventListener('submit', async (event) => {
  event.preventDefault();
  if (!authTokenInput) return;

  const token = authTokenInput.value.trim();
  if (!token) {
    showAuthError('Token is required.');
    return;
  }

  disableAuthForm(true);
  const isValid = await probeToken(token);
  if (!isValid) {
    disableAuthForm(false);
    return;
  }

  saveToken(token);
  hideAuthError();
  authTokenInput.value = '';
  await bootstrapAuthenticatedSession();
  disableAuthForm(false);
});

signOutButton?.addEventListener('click', () => {
  clearToken();
  state.remoteSha = null;
  showAuthSection();
  setSyncStatus('Signed out. Enter token to continue.', 'warning');
});

installButton.addEventListener('click', async () => {
  if (!state.installEvent) return;

  state.installEvent.prompt();
  await state.installEvent.userChoice;
  state.installEvent = null;
  installButton.hidden = true;
});

window.addEventListener('beforeinstallprompt', (event) => {
  event.preventDefault();
  state.installEvent = event;
  installButton.hidden = false;
  installFallback.hidden = true;
});

window.addEventListener('appinstalled', () => {
  state.installEvent = null;
  installButton.hidden = true;
  installFallback.hidden = true;
});

window.addEventListener('load', async () => {
  if ('serviceWorker' in navigator) {
    try {
      await navigator.serviceWorker.register('./sw.js');
    } catch (error) {
      console.error('Service worker registration failed', error);
    }
  }

  if (!('BeforeInstallPromptEvent' in window)) {
    installFallback.hidden = false;
  }

  if (!state.git.owner || !state.git.repo) {
    disableTaskEditor(true);
    showAuthSection();
    setSyncStatus('Missing repository owner/name in runtime-config.js.', 'error');
    return;
  }

  const token = getToken();
  if (!token) {
    disableTaskEditor(true);
    showAuthSection();
    return;
  }

  await bootstrapAuthenticatedSession();
});

render();

async function bootstrapAuthenticatedSession() {
  const token = getToken();
  if (!token) {
    showAuthSection();
    disableTaskEditor(true);
    return;
  }

  const valid = await probeToken(token);
  if (!valid) {
    clearToken();
    showAuthSection();
    disableTaskEditor(true);
    return;
  }

  hideAuthSection();
  disableTaskEditor(false);
  setSyncStatus('Loading tasks from GitHub…', 'pending');

  const loaded = await loadRemoteDocument();
  if (loaded.ok) {
    state.tasks = loaded.document.items.filter((task) => !task.deleted);
    persistLocal();
    render();
    setSyncStatus(`Synced with ${state.git.owner}/${state.git.repo}@${state.git.branch}.`, 'success');
  } else {
    setSyncStatus(loaded.message, 'error');
  }
}

function persistLocal() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state.tasks));
}

async function persistAndRender(commitMessage) {
  persistLocal();
  render();

  state.syncInFlight = state.syncInFlight.then(() => syncToGitHub(commitMessage));
  await state.syncInFlight;
}

function render() {
  taskList.textContent = '';

  if (state.tasks.length === 0) {
    const empty = document.createElement('li');
    empty.className = 'task-item';
    empty.textContent = 'No tasks yet. Add one above.';
    taskList.append(empty);
    return;
  }

  state.tasks.forEach((task) => {
    const item = document.createElement('li');
    item.className = 'task-item';
    item.dataset.taskId = task.id;

    const toggle = document.createElement('input');
    toggle.type = 'checkbox';
    toggle.className = 'toggle';
    toggle.checked = task.completed;
    toggle.setAttribute('aria-label', `Mark ${task.text} as done`);

    const label = document.createElement('button');
    label.type = 'button';
    label.className = `task-label ${task.completed ? 'done' : ''}`;
    label.textContent = task.text;

    const remove = document.createElement('button');
    remove.type = 'button';
    remove.className = 'delete';
    remove.textContent = 'Delete';

    item.append(toggle, label, remove);
    taskList.append(item);
  });
}

async function toggleTask(taskId) {
  let toggled = null;
  state.tasks = state.tasks.map((task) => {
    if (task.id !== taskId) return task;
    toggled = { ...task, completed: !task.completed, updatedAt: new Date().toISOString() };
    return toggled;
  });

  if (!toggled) return;
  await persistAndRender(buildCommitMessage('toggle', toggled.id));
}

async function deleteTask(taskId) {
  const task = state.tasks.find((entry) => entry.id === taskId);
  if (!task) return;

  const confirmed = window.confirm(`Delete "${task.text}"?`);
  if (!confirmed) return;

  state.tasks = state.tasks.filter((entry) => entry.id !== taskId);
  await persistAndRender(buildCommitMessage('delete', task.id));
}

function startInlineEdit(item, taskId) {
  const currentTask = state.tasks.find((task) => task.id === taskId);
  if (!currentTask) return;

  const input = document.createElement('input');
  input.className = 'edit-input';
  input.value = currentTask.text;
  input.setAttribute('aria-label', 'Edit task text');

  const commit = async () => {
    const nextText = input.value.trim();
    if (!nextText) {
      render();
      return;
    }

    state.tasks = state.tasks.map((task) =>
      task.id === taskId ? { ...task, text: nextText, updatedAt: new Date().toISOString() } : task,
    );
    await persistAndRender(buildCommitMessage('edit', taskId));
  };

  input.addEventListener('keydown', async (event) => {
    if (event.key === 'Enter') {
      await commit();
    }

    if (event.key === 'Escape') {
      render();
    }
  });

  input.addEventListener('blur', () => {
    void commit();
  }, { once: true });

  const label = item.querySelector('.task-label');
  if (label) {
    label.replaceWith(input);
    input.focus();
    input.select();
  }
}

function resolveGitConfig(config) {
  const owner = config.repoOwner?.trim() || inferOwnerFromHostname(window.location.hostname);
  const repo = config.repoName?.trim();
  const branch = config.repoBranch?.trim() || 'main';
  const tokenStorageKey = config.auth?.tokenStorageKey?.trim() || 'focus_runtime_token';
  const filePath = normalizeRepoPath(config.repoPath, 'todos.json');

  return { owner, repo, branch, filePath, tokenStorageKey };
}

function normalizeRepoPath(basePath, fileName) {
  const normalizedBase = (basePath || '/').trim().replace(/^\/+|\/+$/g, '');
  return normalizedBase ? `${normalizedBase}/${fileName}` : fileName;
}

function inferOwnerFromHostname(hostname) {
  const match = hostname.match(/^([^\.]+)\.github\.io$/i);
  return match ? match[1] : '';
}

function getToken() {
  const value = localStorage.getItem(state.git.tokenStorageKey);
  return value ? value.trim() : '';
}

function saveToken(token) {
  localStorage.setItem(state.git.tokenStorageKey, token.trim());
}

function clearToken() {
  localStorage.removeItem(state.git.tokenStorageKey);
}

async function probeToken(token) {
  const response = await fetch(`https://api.github.com/repos/${encodeURIComponent(state.git.owner)}/${encodeURIComponent(state.git.repo)}`, {
    headers: {
      Accept: 'application/vnd.github+json',
      Authorization: `Bearer ${token}`,
      'X-GitHub-Api-Version': '2022-11-28',
    },
  });

  if (response.ok) {
    hideAuthError();
    return true;
  }

  if (response.status === 401 || response.status === 403) {
    showAuthError('Token rejected. Use a token with repository read/write permissions.');
  } else if (response.status === 404) {
    showAuthError('Repository not found. Check runtime-config.js owner/repository values.');
  } else {
    showAuthError(`GitHub auth check failed (${response.status}).`);
  }

  return false;
}

async function loadRemoteDocument() {
  const token = getToken();
  const url = buildContentsUrl();
  const response = await fetch(`${url}?ref=${encodeURIComponent(state.git.branch)}`, {
    headers: githubHeaders(token),
  });

  if (response.status === 404) {
    state.remoteSha = null;
    return { ok: true, document: { version: CURRENT_VERSION, items: [] } };
  }

  if (!response.ok) {
    return { ok: false, message: `Failed to load remote tasks (${response.status}).` };
  }

  const payload = await response.json();
  state.remoteSha = payload.sha;

  try {
    const decoded = decodeBase64(payload.content || '');
    const parsed = JSON.parse(decoded);
    return { ok: true, document: normalizeDocument(parsed) };
  } catch (error) {
    console.error(error);
    return { ok: false, message: 'Remote todo file is invalid JSON.' };
  }
}

async function syncToGitHub(commitMessage) {
  const token = getToken();
  if (!token) {
    setSyncStatus('Not synced. Sign in to GitHub to persist tasks.', 'warning');
    return;
  }

  setSyncStatus('Syncing to GitHub…', 'pending');

  const payload = {
    message: commitMessage,
    branch: state.git.branch,
    content: encodeBase64(JSON.stringify({ version: CURRENT_VERSION, items: state.tasks }, null, 2) + '\n'),
  };

  if (state.remoteSha) {
    payload.sha = state.remoteSha;
  }

  const firstTry = await putContents(payload, token);
  if (firstTry.ok) {
    state.remoteSha = firstTry.sha;
    setSyncStatus(`Synced at commit ${firstTry.commitSha.slice(0, 7)}.`, 'success');
    return;
  }

  if (firstTry.status !== 409 && firstTry.status !== 422) {
    setSyncStatus(`Sync failed (${firstTry.status}).`, 'error');
    return;
  }

  const reloaded = await loadRemoteDocument();
  if (!reloaded.ok) {
    setSyncStatus(reloaded.message, 'error');
    return;
  }

  state.tasks = mergeTasks(reloaded.document.items, state.tasks);
  persistLocal();
  render();

  const retryPayload = {
    ...payload,
    sha: state.remoteSha,
    content: encodeBase64(JSON.stringify({ version: CURRENT_VERSION, items: state.tasks }, null, 2) + '\n'),
  };

  const retry = await putContents(retryPayload, token);
  if (!retry.ok) {
    setSyncStatus(`Sync conflict unresolved (${retry.status}).`, 'error');
    return;
  }

  state.remoteSha = retry.sha;
  setSyncStatus(`Synced after merge at ${retry.commitSha.slice(0, 7)}.`, 'success');
}

async function putContents(body, token) {
  const response = await fetch(buildContentsUrl(), {
    method: 'PUT',
    headers: {
      ...githubHeaders(token),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    return { ok: false, status: response.status };
  }

  const data = await response.json();
  return {
    ok: true,
    sha: data.content?.sha || '',
    commitSha: data.commit?.sha || 'unknown',
  };
}

function buildContentsUrl() {
  return `https://api.github.com/repos/${encodeURIComponent(state.git.owner)}/${encodeURIComponent(state.git.repo)}/contents/${encodePath(state.git.filePath)}`;
}

function encodePath(path) {
  return path.split('/').map((segment) => encodeURIComponent(segment)).join('/');
}

function githubHeaders(token) {
  return {
    Accept: 'application/vnd.github+json',
    Authorization: `Bearer ${token}`,
    'X-GitHub-Api-Version': '2022-11-28',
  };
}

function decodeBase64(value) {
  const sanitized = value.replace(/\n/g, '');
  const binary = atob(sanitized);
  const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

function encodeBase64(value) {
  const bytes = new TextEncoder().encode(value);
  let binary = '';
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });
  return btoa(binary);
}

function normalizeDocument(document) {
  if (!document || typeof document !== 'object' || !Array.isArray(document.items)) {
    return { version: CURRENT_VERSION, items: [] };
  }

  return {
    version: Number.isInteger(document.version) ? document.version : CURRENT_VERSION,
    items: document.items
      .filter((item) => item && typeof item.id === 'string' && typeof item.text === 'string')
      .map((item) => ({
        id: item.id,
        text: item.text,
        completed: Boolean(item.completed),
        deleted: Boolean(item.deleted),
        createdAt: typeof item.createdAt === 'string' ? item.createdAt : new Date().toISOString(),
        updatedAt: typeof item.updatedAt === 'string' ? item.updatedAt : new Date().toISOString(),
      })),
  };
}

function mergeTasks(remoteTasks, localTasks) {
  const byId = new Map();

  remoteTasks.forEach((task) => {
    byId.set(task.id, task);
  });

  localTasks.forEach((task) => {
    const remoteTask = byId.get(task.id);
    if (!remoteTask) {
      byId.set(task.id, task);
      return;
    }

    const remoteUpdated = Date.parse(remoteTask.updatedAt || 0);
    const localUpdated = Date.parse(task.updatedAt || 0);
    byId.set(task.id, localUpdated >= remoteUpdated ? task : remoteTask);
  });

  return [...byId.values()].filter((task) => !task.deleted);
}

function disableTaskEditor(disabled) {
  addInput.disabled = disabled;
  addForm.querySelector('button').disabled = disabled;
}

function showAuthSection() {
  authSection.hidden = false;
  signOutButton.hidden = true;
}

function hideAuthSection() {
  authSection.hidden = true;
  signOutButton.hidden = false;
}

function disableAuthForm(disabled) {
  if (!authTokenInput || !authForm) return;
  authTokenInput.disabled = disabled;
  authForm.querySelector('button').disabled = disabled;
}

function showAuthError(message) {
  if (!authError) return;
  authError.hidden = false;
  authError.textContent = message;
}

function hideAuthError() {
  if (!authError) return;
  authError.hidden = true;
  authError.textContent = '';
}

function setSyncStatus(message, tone) {
  statusBar.textContent = message;
  statusBar.dataset.tone = tone;
}

function buildCommitMessage(action, value) {
  const clipped = String(value).trim().replace(/\s+/g, ' ').slice(0, 72);
  return `todo:${action} ${clipped}`;
}

function readLocalTasks() {
  const persisted = localStorage.getItem(STORAGE_KEY);
  if (!persisted) {
    return [];
  }

  try {
    const parsed = JSON.parse(persisted);
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed
      .filter((task) => typeof task.id === 'string' && typeof task.text === 'string')
      .map((task) => ({
        id: task.id,
        text: task.text,
        completed: typeof task.completed === 'boolean' ? task.completed : Boolean(task.done),
        createdAt: typeof task.createdAt === 'string' ? task.createdAt : new Date().toISOString(),
        updatedAt: typeof task.updatedAt === 'string' ? task.updatedAt : new Date().toISOString(),
      }));
  } catch {
    return [];
  }
}
