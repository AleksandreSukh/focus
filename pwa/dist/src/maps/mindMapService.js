import {
  applyMapMutation,
  buildMapSummary,
  compareMapSummariesByRecentUpdate,
  cloneMapDocument,
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

  async loadAttachment(mapFilePath, relativePath, mediaType) {
    return this.repository.loadAttachment(mapFilePath, relativePath, mediaType);
  }

  async deleteMap(filePath, commitMessage) {
    const snapshot = this.snapshotsByPath.get(filePath);
    if (!snapshot) {
      return {
        ok: false,
        error: {
          code: 'NOT_FOUND',
          message: `Map "${filePath}" is not loaded.`,
          retriable: false,
        },
      };
    }

    const deleted = await this.repository.deleteMap(filePath, snapshot.revision, commitMessage);
    if (!deleted.ok) {
      return deleted;
    }

    this.snapshotsByPath.delete(filePath);
    return {
      ok: true,
    };
  }

  async uploadAttachment(mapFilePath, file, commitMessage) {
    const ext = (file.name.includes('.') ? '.' + file.name.split('.').pop() : '').toLowerCase();
    const relativePath = `${createAttachmentId()}${ext}`;
    const base64Content = await readFileAsBase64(file);
    const uploaded = await this.repository.uploadAttachment(
      mapFilePath,
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

  async deleteAttachment(mapFilePath, relativePath, commitMessage) {
    return this.repository.deleteAttachment(mapFilePath, relativePath, null, commitMessage);
  }

  async renameMap(oldFilePath, newFilePath, oldRevision, commitMessage) {
    const snapshot = this.snapshotsByPath.get(newFilePath)
      ?? this.snapshotsByPath.get(oldFilePath);

    if (!snapshot) {
      return {
        ok: false,
        error: {
          code: 'NOT_FOUND',
          message: `Map "${oldFilePath}" is not loaded.`,
          retriable: false,
        },
      };
    }

    const renamed = await this.repository.renameMap(
      oldFilePath,
      newFilePath,
      snapshot.document,
      oldRevision,
      commitMessage,
    );

    if (!renamed.ok) {
      return renamed;
    }

    const newSnapshot = {
      ...snapshot,
      filePath: newFilePath,
      fileName: newFilePath.split('/').pop() || newFilePath,
      mapName: (newFilePath.split('/').pop() || newFilePath).replace(/\.json$/i, ''),
      revision: renamed.revision,
      loadedAt: Date.now(),
    };

    this.snapshotsByPath.delete(oldFilePath);
    this.snapshotsByPath.set(newFilePath, newSnapshot);

    return {
      ok: true,
      value: { snapshot: newSnapshot },
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

    const firstSave = await this.repository.saveMap(
      filePath,
      attemptedDocument,
      latest.value.revision,
      commitMessage,
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

    const retrySave = await this.repository.saveMap(
      filePath,
      retriedDocument,
      refreshed.value.revision,
      commitMessage,
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
