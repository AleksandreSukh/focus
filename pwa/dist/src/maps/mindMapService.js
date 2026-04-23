import {
  applyMapMutation,
  buildMapSummary,
  compareMapSummariesByRecentUpdate,
  cloneMapDocument,
  collectDocumentAttachmentRefs,
  collectTaskEntries,
  createMapDocument,
} from './model.js';

export class MindMapService {
  constructor(repository) {
    this.repository = repository;
    this.snapshotsByPath = new Map();
  }

  async listMaps(forceRefresh = false) {
    const listed = await this.repository.listFiles();
    if (!listed.ok) {
      return listed;
    }

    const loadedSnapshots = [];
    const unreadableMaps = [];
    for (const file of listed.value) {
      const loaded = await this.loadMap(file.filePath, forceRefresh);
      if (!loaded.ok) {
        if (loaded.error?.code === 'UNREADABLE_MAP') {
          this.snapshotsByPath.delete(file.filePath);
          unreadableMaps.push(buildUnreadableMapEntry(loaded.error));
          continue;
        }

        return loaded;
      }

      loadedSnapshots.push(loaded.value);
    }

    return {
      ok: true,
      value: {
        snapshots: loadedSnapshots,
        unreadableMaps: unreadableMaps.sort(compareUnreadableMapEntries),
      },
    };
  }

  async loadMap(filePath, forceRefresh = false) {
    if (!forceRefresh && this.snapshotsByPath.has(filePath)) {
      return {
        ok: true,
        value: this.snapshotsByPath.get(filePath),
      };
    }

    const loaded = await this.repository.loadMap(filePath);
    if (!loaded.ok) {
      if (loaded.error?.code === 'UNREADABLE_MAP') {
        this.snapshotsByPath.delete(filePath);
      }
      return loaded;
    }

    this.snapshotsByPath.set(filePath, loaded.value);
    return loaded;
  }

  getCachedSnapshot(filePath) {
    return this.snapshotsByPath.get(filePath) ?? null;
  }

  getCachedSnapshots() {
    return Array.from(this.snapshotsByPath.values());
  }

  replaceCachedSnapshot(snapshot) {
    if (!snapshot?.filePath) {
      return;
    }

    this.snapshotsByPath.set(snapshot.filePath, snapshot);
  }

  removeCachedSnapshot(filePath) {
    if (!filePath) {
      return;
    }

    this.snapshotsByPath.delete(filePath);
  }

  hydrateSnapshots(snapshots) {
    this.snapshotsByPath.clear();
    snapshots.forEach((snapshot) => {
      if (snapshot?.filePath) {
        this.snapshotsByPath.set(snapshot.filePath, snapshot);
      }
    });
  }

  buildSummaries(snapshots) {
    return snapshots
      .map((snapshot) => buildMapSummary(snapshot))
      .sort(compareMapSummariesByRecentUpdate);
  }

  buildTaskEntries(filter = 'open', snapshots = Array.from(this.snapshotsByPath.values())) {
    return snapshots.flatMap((snapshot) => collectTaskEntries(snapshot, filter));
  }

  async createMap(filePath, mapName, commitMessage) {
    const document = createMapDocument(mapName);
    const saved = await this.repository.createMap(filePath, document, commitMessage);
    if (!saved.ok) {
      return saved;
    }

    const fileName = filePath.split('/').pop() || filePath;
    const snapshot = {
      filePath,
      fileName,
      mapName,
      document,
      revision: saved.revision,
      loadedAt: Date.now(),
    };
    this.snapshotsByPath.set(filePath, snapshot);
    return {
      ok: true,
      value: snapshot,
    };
  }

  async loadAttachment(mapFilePath, nodeId, relativePath, mediaType) {
    return this.repository.loadAttachment(mapFilePath, nodeId, relativePath, mediaType);
  }

  async deleteMap(filePath, commitMessage) {
    const latest = await this.loadMap(filePath, false);
    if (!latest.ok) {
      return latest;
    }

    const firstDelete = await persistMapDeleteAttempt(
      this.repository,
      latest.value,
      commitMessage,
    );
    if (firstDelete.ok) {
      this.snapshotsByPath.delete(filePath);
      return {
        ok: true,
      };
    }

    if (firstDelete.error.code !== 'STALE_STATE') {
      return firstDelete;
    }

    const refreshed = await this.loadMap(filePath, true);
    if (!refreshed.ok) {
      return refreshed;
    }

    const retryDelete = await persistMapDeleteAttempt(
      this.repository,
      refreshed.value,
      commitMessage,
    );
    if (!retryDelete.ok) {
      return retryDelete;
    }

    this.snapshotsByPath.delete(filePath);
    return {
      ok: true,
    };
  }

  async uploadAttachment(mapFilePath, nodeId, file, commitMessage) {
    const ext = (file.name.includes('.') ? '.' + file.name.split('.').pop() : '').toLowerCase();
    const relativePath = `${createAttachmentId()}${ext}`;
    const base64Content = await readFileAsBase64(file);
    const uploaded = await this.repository.uploadAttachment(
      mapFilePath,
      nodeId,
      relativePath,
      base64Content,
      commitMessage,
    );
    if (!uploaded.ok) {
      return uploaded;
    }

    return {
      ok: true,
      value: {
        id: relativePath.replace(/\.[^.]+$/, ''),
        relativePath,
        mediaType: file.type || 'application/octet-stream',
        displayName: file.name,
        createdAtUtc: new Date().toISOString(),
      },
    };
  }

  async deleteAttachment(mapFilePath, nodeId, relativePath, commitMessage) {
    return this.repository.deleteAttachment(mapFilePath, nodeId, relativePath, null, commitMessage);
  }

  async renameMap(operationOrOldFilePath, newFilePathArg, oldRevisionArg, commitMessageArg) {
    const operation = normalizeRenameOperation(
      operationOrOldFilePath,
      newFilePathArg,
      oldRevisionArg,
      commitMessageArg,
    );
    const snapshot = this.snapshotsByPath.get(operation.newFilePath)
      ?? this.snapshotsByPath.get(operation.filePath);

    if (!snapshot) {
      return {
        ok: false,
        error: {
          code: 'NOT_FOUND',
          message: `Map "${operation.filePath}" is not loaded.`,
          retriable: false,
        },
      };
    }

    let nextDocument = snapshot.document;
    let mutation = null;
    if (operation.nodeId && typeof operation.text === 'string') {
      nextDocument = cloneMapDocument(snapshot.document);
      const applied = applyMapMutation(nextDocument, {
        type: 'editNodeText',
        nodeId: operation.nodeId,
        text: operation.text,
        timestamp: operation.timestamp,
      });
      if (!applied.ok) {
        return {
          ok: false,
          error: {
            ...applied.error,
            meta: {
              filePath: operation.filePath,
              mutation: {
                type: 'editNodeText',
                nodeId: operation.nodeId,
                text: operation.text,
                timestamp: operation.timestamp,
              },
              commitMessage: operation.commitMessage,
            },
          },
        };
      }

      mutation = applied.value;
    }

    const renamed = await this.repository.renameMap(
      operation.filePath,
      operation.newFilePath,
      nextDocument,
      operation.oldRevision,
      operation.commitMessage,
    );

    if (!renamed.ok) {
      return renamed;
    }

    const newSnapshot = {
      ...snapshot,
      filePath: operation.newFilePath,
      fileName: operation.newFilePath.split('/').pop() || operation.newFilePath,
      mapName: (operation.newFilePath.split('/').pop() || operation.newFilePath).replace(/\.json$/i, ''),
      document: nextDocument,
      revision: renamed.revision,
      loadedAt: Date.now(),
    };

    this.snapshotsByPath.delete(operation.filePath);
    this.snapshotsByPath.set(operation.newFilePath, newSnapshot);

    return {
      ok: true,
      value: { snapshot: newSnapshot, mutation },
    };
  }

  async saveResolved(filePath, document, revision, commitMessage) {
    const saved = await this.repository.saveMap(filePath, document, revision, commitMessage);
    if (!saved.ok) {
      return saved;
    }

    const existing = this.snapshotsByPath.get(filePath);
    const fileName = existing?.fileName || filePath.split('/').pop() || filePath;
    const mapName = existing?.mapName || fileName.replace(/\.json$/i, '');

    const snapshot = {
      filePath,
      fileName,
      mapName,
      document,
      revision: saved.revision,
      loadedAt: Date.now(),
    };
    this.snapshotsByPath.set(filePath, snapshot);
    return {
      ok: true,
      value: snapshot,
    };
  }

  async applyMutation(filePath, mutation, commitMessage) {
    const latest = await this.loadMap(filePath, false);
    if (!latest.ok) {
      return latest;
    }

    const attemptedDocument = cloneMapDocument(latest.value.document);
    const applied = applyMapMutation(attemptedDocument, mutation);
    if (!applied.ok) {
      return {
        ok: false,
        error: {
          ...applied.error,
          meta: {
            filePath,
            mutation,
            commitMessage,
          },
        },
      };
    }

    const firstSave = await persistMutationAttempt(
      this.repository,
      filePath,
      attemptedDocument,
      latest.value.revision,
      commitMessage,
      applied.value,
    );
    if (firstSave.ok) {
      const snapshot = {
        ...latest.value,
        document: attemptedDocument,
        revision: firstSave.revision,
        loadedAt: Date.now(),
      };
      this.snapshotsByPath.set(filePath, snapshot);
      return {
        ok: true,
        value: {
          snapshot,
          mutation: applied.value,
        },
      };
    }

    if (firstSave.error.code !== 'STALE_STATE') {
      return firstSave;
    }

    const refreshed = await this.loadMap(filePath, true);
    if (!refreshed.ok) {
      return refreshed;
    }

    const retriedDocument = cloneMapDocument(refreshed.value.document);
    const retriedApply = applyMapMutation(retriedDocument, mutation);
    if (!retriedApply.ok) {
      return {
        ok: false,
        error: {
          ...retriedApply.error,
          meta: {
            filePath,
            mutation,
            commitMessage,
          },
        },
      };
    }

    const retrySave = await persistMutationAttempt(
      this.repository,
      filePath,
      retriedDocument,
      refreshed.value.revision,
      commitMessage,
      retriedApply.value,
    );
    if (!retrySave.ok) {
      return {
        ok: false,
        error: {
          code: 'CONFLICT_UNRESOLVED',
          message: `Map "${filePath}" conflicted twice and needs manual resolution.`,
          retriable: false,
          cause: retrySave.error,
          meta: {
            filePath,
            mutation,
            commitMessage,
          },
        },
      };
    }

    const snapshot = {
      ...refreshed.value,
      document: retriedDocument,
      revision: retrySave.revision,
      loadedAt: Date.now(),
    };
    this.snapshotsByPath.set(filePath, snapshot);
    return {
      ok: true,
      value: {
        snapshot,
        mutation: retriedApply.value,
      },
    };
  }
}

async function persistMutationAttempt(repository, filePath, document, revision, commitMessage, mutationResult) {
  const deletedAttachments = normalizeAttachmentRefs(mutationResult?.deletedAttachments);
  for (const attachment of deletedAttachments) {
    const deleted = await repository.deleteAttachment(
      filePath,
      attachment.nodeId,
      attachment.relativePath,
      null,
      commitMessage,
    );
    if (!deleted.ok) {
      return deleted;
    }
  }

  return repository.saveMap(filePath, document, revision, commitMessage);
}

async function persistMapDeleteAttempt(repository, snapshot, commitMessage) {
  const attachmentRefs = normalizeAttachmentRefs(collectDocumentAttachmentRefs(snapshot.document));
  for (const attachment of attachmentRefs) {
    const deleted = await repository.deleteAttachment(
      snapshot.filePath,
      attachment.nodeId,
      attachment.relativePath,
      null,
      commitMessage,
    );
    if (!deleted.ok) {
      return deleted;
    }
  }

  return repository.deleteMap(snapshot.filePath, snapshot.revision, commitMessage);
}

function normalizeAttachmentRefs(attachmentRefs) {
  const refs = Array.isArray(attachmentRefs)
    ? attachmentRefs
    : [];
  const seen = new Set();

  return refs.filter((attachment) => {
    const nodeId = typeof attachment?.nodeId === 'string' ? attachment.nodeId : '';
    const relativePath = typeof attachment?.relativePath === 'string' ? attachment.relativePath : '';
    if (!nodeId || !relativePath) {
      return false;
    }

    const key = `${nodeId}\u0000${relativePath}`;
    if (seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}

function normalizeRenameOperation(operationOrOldFilePath, newFilePath, oldRevision, commitMessage) {
  if (operationOrOldFilePath && typeof operationOrOldFilePath === 'object') {
    return {
      filePath: String(operationOrOldFilePath.filePath ?? ''),
      newFilePath: String(operationOrOldFilePath.newFilePath ?? ''),
      oldRevision: String(operationOrOldFilePath.oldRevision ?? ''),
      commitMessage: String(operationOrOldFilePath.commitMessage ?? ''),
      nodeId: String(operationOrOldFilePath.nodeId ?? ''),
      text: typeof operationOrOldFilePath.text === 'string' ? operationOrOldFilePath.text : '',
      timestamp: typeof operationOrOldFilePath.timestamp === 'string' ? operationOrOldFilePath.timestamp : '',
    };
  }

  return {
    filePath: String(operationOrOldFilePath ?? ''),
    newFilePath: String(newFilePath ?? ''),
    oldRevision: String(oldRevision ?? ''),
    commitMessage: String(commitMessage ?? ''),
    nodeId: '',
    text: '',
    timestamp: '',
  };
}

function createAttachmentId() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID();
  }

  return `${Date.now().toString(16).padStart(12, '0')}-${Math.random().toString(16).slice(2, 14).padEnd(12, '0')}`;
}

function readFileAsBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result;
      if (typeof result !== 'string') {
        reject(new Error('FileReader did not return a string.'));
        return;
      }
      // result is "data:<mediaType>;base64,<data>" — strip the prefix
      const commaIndex = result.indexOf(',');
      resolve(commaIndex >= 0 ? result.slice(commaIndex + 1) : result);
    };
    reader.onerror = () => reject(reader.error || new Error('Failed to read file.'));
    reader.readAsDataURL(file);
  });
}

function buildUnreadableMapEntry(error) {
  return {
    filePath: error.filePath,
    fileName: error.fileName,
    mapName: error.mapName,
    revision: error.revision,
    reason: error.reason,
    message: error.message,
    rawText: error.rawText,
  };
}

function compareUnreadableMapEntries(left, right) {
  return String(left?.fileName ?? '').localeCompare(String(right?.fileName ?? ''));
}
