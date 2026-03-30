function normalizeValue(value) {
  return String(value ?? '').trim().replace(/\s+/g, ' ');
}

function truncate(value, maxLength) {
  return value.length > maxLength ? value.slice(0, maxLength).trimEnd() : value;
}

export function buildTodoAddCommitMessage(shortText) {
  const normalized = truncate(normalizeValue(shortText), 72);
  return `todo:add ${normalized}`;
}

export function buildTodoEditCommitMessage(id) {
  return `todo:edit ${normalizeValue(id)}`;
}

export function buildTodoToggleCommitMessage(id, completed) {
  return `todo:toggle ${normalizeValue(id)} -> ${completed ? 'done' : 'open'}`;
}

export function buildTodoDeleteCommitMessage(id) {
  return `todo:delete ${normalizeValue(id)}`;
}
