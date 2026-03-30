const STORAGE_KEY = 'focus-pwa-tasks-v1';
const state = {
  tasks: readTasks(),
  installEvent: null,
};

const taskList = document.getElementById('task-list');
const addForm = document.getElementById('add-form');
const addInput = document.getElementById('add-input');
const installButton = document.getElementById('install-button');
const installFallback = document.getElementById('install-fallback');

addForm.addEventListener('submit', (event) => {
  event.preventDefault();
  const value = addInput.value.trim();
  if (!value) return;

  state.tasks.unshift({
    id: crypto.randomUUID(),
    text: value,
    done: false,
  });

  addInput.value = '';
  persistAndRender();
  addInput.focus();
});

taskList.addEventListener('click', (event) => {
  const target = event.target;
  if (!(target instanceof HTMLElement)) return;

  const item = target.closest('[data-task-id]');
  if (!(item instanceof HTMLElement)) return;
  const taskId = item.dataset.taskId;
  if (!taskId) return;

  if (target.matches('.toggle')) {
    toggleTask(taskId);
    return;
  }

  if (target.matches('.delete')) {
    deleteTask(taskId);
    return;
  }

  if (target.matches('.task-label')) {
    startInlineEdit(item, taskId);
  }
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
});

render();

function persistAndRender() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state.tasks));
  render();
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
    toggle.checked = task.done;
    toggle.setAttribute('aria-label', `Mark ${task.text} as done`);

    const label = document.createElement('button');
    label.type = 'button';
    label.className = `task-label ${task.done ? 'done' : ''}`;
    label.textContent = task.text;

    const remove = document.createElement('button');
    remove.type = 'button';
    remove.className = 'delete';
    remove.textContent = 'Delete';

    item.append(toggle, label, remove);
    taskList.append(item);
  });
}

function toggleTask(taskId) {
  state.tasks = state.tasks.map((task) =>
    task.id === taskId ? { ...task, done: !task.done } : task,
  );
  persistAndRender();
}

function deleteTask(taskId) {
  const task = state.tasks.find((entry) => entry.id === taskId);
  if (!task) return;

  const confirmed = window.confirm(`Delete "${task.text}"?`);
  if (!confirmed) return;

  state.tasks = state.tasks.filter((entry) => entry.id !== taskId);
  persistAndRender();
}

function startInlineEdit(item, taskId) {
  const currentTask = state.tasks.find((task) => task.id === taskId);
  if (!currentTask) return;

  const input = document.createElement('input');
  input.className = 'edit-input';
  input.value = currentTask.text;
  input.setAttribute('aria-label', 'Edit task text');

  const commit = () => {
    const nextText = input.value.trim();
    if (!nextText) {
      render();
      return;
    }

    state.tasks = state.tasks.map((task) =>
      task.id === taskId ? { ...task, text: nextText } : task,
    );
    persistAndRender();
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

  const label = item.querySelector('.task-label');
  if (label) {
    label.replaceWith(input);
    input.focus();
    input.select();
  }
}

function readTasks() {
  const persisted = localStorage.getItem(STORAGE_KEY);
  if (!persisted) {
    return [];
  }

  try {
    const parsed = JSON.parse(persisted);
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.filter((task) => typeof task.id === 'string' && typeof task.text === 'string' && typeof task.done === 'boolean');
  } catch {
    return [];
  }
}
