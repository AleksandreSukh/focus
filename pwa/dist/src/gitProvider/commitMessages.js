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

function normalizeMapName(mapName) {
  const normalized = truncate(normalizeValue(mapName), 48);
  return normalized || 'map';
}

export function buildNodeAddCommitMessage(mapName, shortText, kind = 'note') {
  const normalizedKind = kind === 'task' ? 'task' : 'note';
  const normalizedText = truncate(normalizeValue(shortText), 48);
  return `map:add ${normalizedKind} ${normalizeMapName(mapName)} ${normalizedText}`.trimEnd();
}

export function buildNodeEditCommitMessage(mapName, nodeId) {
  return `map:edit ${normalizeMapName(mapName)} ${normalizeValue(nodeId)}`;
}

export function buildNodeTaskStateCommitMessage(mapName, nodeId, taskState) {
  const stateLabel =
    taskState === 1 ? 'todo' :
      taskState === 2 ? 'doing' :
        taskState === 3 ? 'done' :
          'clear';
  return `map:task ${normalizeMapName(mapName)} ${normalizeValue(nodeId)} -> ${stateLabel}`;
}
