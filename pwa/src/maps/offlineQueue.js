import {
  applyMapMutation,
  cloneMapDocument,
  createMapDocument,
  normalizeMindMapDocument,
  nowIso,
} from './model.js';

export function buildCreateMapOperation({
  filePath,
  mapName,
  commitMessage,
  timestamp = nowIso(),
}) {
  const document = createMapDocument(mapName);
  return {
    type: 'createMap',
    filePath: String(filePath ?? ''),
    mapName: String(mapName ?? ''),
    document,
    timestamp,
    commitMessage: String(commitMessage ?? ''),
  };
}

export function buildDeleteMapOperation({
  snapshot,
  commitMessage,
  timestamp = nowIso(),
}) {
  return {
    type: 'deleteMap',
    filePath: String(snapshot?.filePath ?? ''),
    mapName: String(snapshot?.mapName ?? ''),
    revision: String(snapshot?.revision ?? ''),
    timestamp,
    commitMessage: String(commitMessage ?? ''),
  };
}

export function buildSnapshotFromCreateMapOperation(operation, loadedAt = Date.now()) {
  const filePath = String(operation?.filePath ?? '');
  const fileName = filePath.split('/').pop() || filePath;
  const mapName = String(operation?.mapName || fileName.replace(/\.json$/i, ''));
  const rawDocument = operation?.document && typeof operation.document === 'object'
    ? cloneMapDocument(operation.document)
    : createMapDocument(mapName);
  const document = normalizeMindMapDocument(rawDocument);

  return {
    filePath,
    fileName,
    mapName,
    document,
    revision: typeof operation?.revision === 'string' ? operation.revision : '',
    loadedAt,
  };
}

export function buildRenamedSnapshot(snapshot, operation, loadedAt = Date.now()) {
  const nextDocument = cloneMapDocument(snapshot.document);
  const applied = applyMapMutation(nextDocument, {
    type: 'editNodeText',
    nodeId: operation.nodeId,
    text: operation.text,
    timestamp: operation.timestamp,
  });
  if (!applied.ok) {
    return applied;
  }

  const nextFilePath = operation.newFilePath || snapshot.filePath;
  const nextFileName = nextFilePath.split('/').pop() || nextFilePath;
  return {
    ok: true,
    value: {
      snapshot: {
        ...snapshot,
        filePath: nextFilePath,
        fileName: nextFileName,
        mapName: nextFileName.replace(/\.json$/i, ''),
        document: nextDocument,
        loadedAt,
      },
      mutation: applied.value,
    },
  };
}

export function applyPendingOperationsLocally(snapshots, pendingOperations, loadedAt = Date.now()) {
  const snapshotsByPath = new Map(
    (Array.isArray(snapshots) ? snapshots : []).map((snapshot) => [
      snapshot.filePath,
      {
        ...snapshot,
        document: cloneMapDocument(snapshot.document),
      },
    ]),
  );

  (Array.isArray(pendingOperations) ? pendingOperations : []).forEach((operation) => {
    if (operation?.type === 'createMap') {
      const snapshot = buildSnapshotFromCreateMapOperation(operation, loadedAt);
      if (snapshot.filePath) {
        snapshotsByPath.set(snapshot.filePath, snapshot);
      }
      return;
    }

    if (operation?.type === 'deleteMap') {
      snapshotsByPath.delete(operation.filePath);
      return;
    }

    if (operation?.type === 'renameMap') {
      const snapshot = snapshotsByPath.get(operation.filePath) ?? snapshotsByPath.get(operation.newFilePath);
      if (!snapshot) {
        return;
      }

      const renamed = buildRenamedSnapshot(snapshot, operation, loadedAt);
      if (!renamed.ok) {
        return;
      }

      snapshotsByPath.delete(operation.filePath);
      snapshotsByPath.delete(operation.newFilePath);
      snapshotsByPath.set(renamed.value.snapshot.filePath, renamed.value.snapshot);
      return;
    }

    const snapshot = snapshotsByPath.get(operation?.filePath);
    if (!snapshot) {
      return;
    }

    const applied = applyMapMutation(snapshot.document, operation);
    if (applied.ok) {
      snapshot.loadedAt = loadedAt;
    }
  });

  return Array.from(snapshotsByPath.values());
}

export function isTransientRemoteError(error) {
  const errorCode = error?.cause?.code || error?.code;
  if (errorCode === 'UNAUTHORIZED' || errorCode === 'FORBIDDEN' || errorCode === 'NOT_FOUND') {
    return false;
  }

  return (
    errorCode === 'NETWORK' ||
    errorCode === 'RATE_LIMIT' ||
    Boolean(error?.retriable)
  );
}

export function canUseCachedWorkspace(error, snapshots, pendingOperations, workspaceInitialized = false) {
  const hasCachedState =
    (Array.isArray(snapshots) && snapshots.length > 0) ||
    (Array.isArray(pendingOperations) && pendingOperations.length > 0) ||
    Boolean(workspaceInitialized);
  return hasCachedState && isTransientRemoteError(error);
}
