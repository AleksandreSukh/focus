const TODO_CACHE_PREFIX = 'focus.pwa.todos.cache.';
const TODO_QUEUE_PREFIX = 'focus.pwa.todos.queue.';

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

export function loadCachedTodos(scope) {
  if (!hasLocalStorage()) {
    return [];
  }

  const parsed = safeParse(
    window.localStorage.getItem(resolveScopedKey(TODO_CACHE_PREFIX, scope)),
    [],
  );
  if (!Array.isArray(parsed)) {
    return [];
  }

  return parsed
    .filter((todo) => todo && typeof todo.id === 'string' && typeof todo.text === 'string')
    .map((todo) => ({
      id: todo.id,
      text: todo.text,
      completed: Boolean(todo.completed),
      deleted: Boolean(todo.deleted),
      createdAt: typeof todo.createdAt === 'string' ? todo.createdAt : new Date().toISOString(),
      updatedAt: typeof todo.updatedAt === 'string' ? todo.updatedAt : new Date().toISOString(),
    }))
    .filter((todo) => !todo.deleted);
}

export function saveCachedTodos(scope, todos) {
  if (!hasLocalStorage()) {
    return;
  }

  window.localStorage.setItem(
    resolveScopedKey(TODO_CACHE_PREFIX, scope),
    JSON.stringify(Array.isArray(todos) ? todos : []),
  );
}

export function loadPendingOperations(scope) {
  if (!hasLocalStorage()) {
    return [];
  }

  const parsed = safeParse(
    window.localStorage.getItem(resolveScopedKey(TODO_QUEUE_PREFIX, scope)),
    [],
  );
  return Array.isArray(parsed) ? parsed : [];
}

export function savePendingOperations(scope, operations) {
  if (!hasLocalStorage()) {
    return;
  }

  window.localStorage.setItem(
    resolveScopedKey(TODO_QUEUE_PREFIX, scope),
    JSON.stringify(Array.isArray(operations) ? operations : []),
  );
}
