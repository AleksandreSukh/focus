const normalizeValue = (value: string): string => value.trim().replace(/\s+/g, ' ');

const truncate = (value: string, maxLength: number): string =>
  value.length > maxLength ? value.slice(0, maxLength).trimEnd() : value;

const normalizeMapName = (mapName: string): string =>
  truncate(normalizeValue(mapName), 48) || 'map';

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
  `map:rename ${normalizeMapName(oldMapName)} -> ${normalizeMapName(newMapName)}`;

export const buildNodeTaskStateCommitMessage = (mapName: string, nodeId: string, taskState: number): string => {
  const stateLabel =
    taskState === 1 ? 'todo' :
      taskState === 2 ? 'doing' :
        taskState === 3 ? 'done' :
          'clear';
  return `map:task ${normalizeMapName(mapName)} ${normalizeValue(nodeId)} -> ${stateLabel}`;
};

export const buildNodeHideDoneTasksCommitMessage = (mapName: string, nodeId: string, hideDoneTasks: boolean): string =>
  `map:hide-done ${normalizeMapName(mapName)} ${normalizeValue(nodeId)} -> ${hideDoneTasks ? 'hide' : 'show'}`;

export const buildNodeStarCommitMessage = (mapName: string, nodeId: string, starred: boolean): string =>
  `map:star ${normalizeMapName(mapName)} ${normalizeValue(nodeId)} -> ${starred ? 'starred' : 'unstarred'}`;

export const buildAttachmentAddCommitMessage = (mapName: string, fileName: string): string =>
  `map:attach ${normalizeMapName(mapName)} ${truncate(normalizeValue(fileName), 48)}`.trimEnd();

export const buildAttachmentRemoveCommitMessage = (mapName: string, fileName: string): string =>
  `map:detach ${normalizeMapName(mapName)} ${truncate(normalizeValue(fileName), 48)}`.trimEnd();
