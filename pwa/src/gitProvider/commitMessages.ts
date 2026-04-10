const normalizeValue = (value: string): string => value.trim().replace(/\s+/g, ' ');

const truncate = (value: string, maxLength: number): string =>
  value.length > maxLength ? value.slice(0, maxLength).trimEnd() : value;

export const buildTodoAddCommitMessage = (shortText: string): string => {
  const normalized = truncate(normalizeValue(shortText), 72);
  return `todo:add ${normalized}`;
};

export const buildTodoEditCommitMessage = (id: string): string =>
  `todo:edit ${normalizeValue(id)}`;

export const buildTodoToggleCommitMessage = (
  id: string,
  completed: boolean,
): string => `todo:toggle ${normalizeValue(id)} -> ${completed ? 'done' : 'open'}`;

export const buildTodoDeleteCommitMessage = (id: string): string =>
  `todo:delete ${normalizeValue(id)}`;

export const buildMapRenameCommitMessage = (oldMapName: string, newMapName: string): string =>
  `map:rename ${truncate(normalizeValue(oldMapName), 48) || 'map'} -> ${truncate(normalizeValue(newMapName), 48) || 'map'}`;
