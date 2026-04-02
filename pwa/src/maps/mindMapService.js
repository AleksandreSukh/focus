import {
  applyMapMutation,
  buildMapSummary,
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
    for (const file of listed.value) {
      const loaded = await this.loadMap(file.filePath, forceRefresh);
      if (!loaded.ok) {
        return loaded;
      }

      loadedSnapshots.push(loaded.value);
    }

    return {
      ok: true,
      value: loadedSnapshots,
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
      return loaded;
    }

    this.snapshotsByPath.set(filePath, loaded.value);
    return loaded;
  }

  getCachedSnapshot(filePath) {
    return this.snapshotsByPath.get(filePath) ?? null;
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
      .sort((left, right) => left.fileName.localeCompare(right.fileName));
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
