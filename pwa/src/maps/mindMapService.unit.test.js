import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { MindMapService } from './mindMapService.js';
import { applyMapMutation, createMapDocument } from './model.js';

function createSnapshot({
  fileName,
  mapName,
  updatedAt,
  rootUpdatedAt,
}) {
  const document = createMapDocument(mapName);
  document.rootNode.metadata.updatedAtUtc = rootUpdatedAt ?? updatedAt ?? document.rootNode.metadata.updatedAtUtc;

  if (typeof updatedAt === 'string') {
    document.updatedAt = updatedAt;
  } else {
    delete document.updatedAt;
  }

  return {
    filePath: `FocusMaps/${fileName}`,
    fileName,
    mapName,
    document,
    revision: 'rev-1',
    loadedAt: 0,
  };
}

function createRepository(overrides = {}) {
  return {
    async listFiles() {
      return overrides.listFiles
        ? overrides.listFiles()
        : {
            ok: true,
            value: [],
          };
    },
    async loadMap(filePath) {
      return overrides.loadMap
        ? overrides.loadMap(filePath)
        : {
            ok: false,
            error: {
              code: 'NOT_IMPLEMENTED',
              message: `No mock loadMap result for "${filePath}".`,
            },
          };
    },
  };
}

describe('MindMapService.buildSummaries', () => {
  it('sorts maps by updatedAt descending', () => {
    const service = new MindMapService(null);
    const summaries = service.buildSummaries([
      createSnapshot({
        fileName: 'older.json',
        mapName: 'Older',
        updatedAt: '2026-04-09T10:00:00Z',
      }),
      createSnapshot({
        fileName: 'newer.json',
        mapName: 'Newer',
        updatedAt: '2026-04-09T11:00:00Z',
      }),
    ]);

    assert.deepEqual(summaries.map((summary) => summary.fileName), ['newer.json', 'older.json']);
  });

  it('uses filename order as a deterministic tie-breaker', () => {
    const service = new MindMapService(null);
    const summaries = service.buildSummaries([
      createSnapshot({
        fileName: 'bravo.json',
        mapName: 'Bravo',
        updatedAt: '2026-04-09T10:00:00Z',
      }),
      createSnapshot({
        fileName: 'alpha.json',
        mapName: 'Alpha',
        updatedAt: '2026-04-09T10:00:00Z',
      }),
    ]);

    assert.deepEqual(summaries.map((summary) => summary.fileName), ['alpha.json', 'bravo.json']);
  });

  it('falls back to the root-node timestamp when the map timestamp is missing', () => {
    const service = new MindMapService(null);
    const summaries = service.buildSummaries([
      createSnapshot({
        fileName: 'legacy-older.json',
        mapName: 'Legacy Older',
        rootUpdatedAt: '2026-04-09T09:00:00Z',
      }),
      createSnapshot({
        fileName: 'legacy-newer.json',
        mapName: 'Legacy Newer',
        rootUpdatedAt: '2026-04-09T12:00:00Z',
      }),
    ]);

    assert.deepEqual(summaries.map((summary) => summary.fileName), ['legacy-newer.json', 'legacy-older.json']);
  });

  it('moves a locally edited map to the top immediately after its timestamp changes', () => {
    const service = new MindMapService(null);
    const laterSnapshot = createSnapshot({
      fileName: 'later.json',
      mapName: 'Later',
      updatedAt: '2026-04-09T11:00:00Z',
    });
    const editedSnapshot = createSnapshot({
      fileName: 'edited.json',
      mapName: 'Edited',
      updatedAt: '2026-04-09T10:00:00Z',
    });

    assert.deepEqual(
      service.buildSummaries([editedSnapshot, laterSnapshot]).map((summary) => summary.fileName),
      ['later.json', 'edited.json'],
    );

    const editResult = applyMapMutation(editedSnapshot.document, {
      type: 'editNodeText',
      nodeId: editedSnapshot.document.rootNode.uniqueIdentifier,
      text: 'Edited root',
      timestamp: '2026-04-09T13:00:00Z',
    });

    assert.equal(editResult.ok, true);
    assert.deepEqual(
      service.buildSummaries([editedSnapshot, laterSnapshot]).map((summary) => summary.fileName),
      ['edited.json', 'later.json'],
    );
  });
});

describe('MindMapService.listMaps', () => {
  it('returns readable snapshots alongside unreadable maps', async () => {
    const readableSnapshot = createSnapshot({
      fileName: 'readable.json',
      mapName: 'Readable',
      updatedAt: '2026-04-09T11:00:00Z',
    });
    const repository = createRepository({
      listFiles: async () => ({
        ok: true,
        value: [
          {
            filePath: readableSnapshot.filePath,
            fileName: readableSnapshot.fileName,
          },
          {
            filePath: 'FocusMaps/conflicted.json',
            fileName: 'conflicted.json',
          },
        ],
      }),
      loadMap: async (filePath) => {
        if (filePath === readableSnapshot.filePath) {
          return {
            ok: true,
            value: readableSnapshot,
          };
        }

        return {
          ok: false,
          error: {
            code: 'UNREADABLE_MAP',
            filePath,
            fileName: 'conflicted.json',
            mapName: 'conflicted',
            revision: 'rev-conflict',
            reason: 'autoResolveFailed',
            message: `This map has merge conflicts that couldn't be auto-resolved. Repair locally or reset it from GitHub.`,
            rawText: '<<<<<<< HEAD\nold\n=======\nnew\n>>>>>>> main\n',
          },
        };
      },
    });
    const service = new MindMapService(repository);

    const listed = await service.listMaps(true);

    assert.equal(listed.ok, true);
    assert.deepEqual(
      listed.value.snapshots.map((snapshot) => snapshot.fileName),
      ['readable.json'],
    );
    assert.deepEqual(listed.value.unreadableMaps, [
      {
        filePath: 'FocusMaps/conflicted.json',
        fileName: 'conflicted.json',
        mapName: 'conflicted',
        revision: 'rev-conflict',
        reason: 'autoResolveFailed',
        message: `This map has merge conflicts that couldn't be auto-resolved. Repair locally or reset it from GitHub.`,
        rawText: '<<<<<<< HEAD\nold\n=======\nnew\n>>>>>>> main\n',
      },
    ]);
    assert.equal(service.getCachedSnapshot('FocusMaps/conflicted.json'), null);
  });

  it('keeps successfully loaded maps out of unreadableMaps even when their filenames were previously conflicted', async () => {
    const conflictedSnapshot = createSnapshot({
      fileName: 'conflicted.json',
      mapName: 'Conflicted',
      updatedAt: '2026-04-09T12:00:00Z',
    });
    const repository = createRepository({
      listFiles: async () => ({
        ok: true,
        value: [
          {
            filePath: conflictedSnapshot.filePath,
            fileName: conflictedSnapshot.fileName,
          },
        ],
      }),
      loadMap: async () => ({
        ok: true,
        value: conflictedSnapshot,
      }),
    });
    const service = new MindMapService(repository);

    const listed = await service.listMaps(true);

    assert.equal(listed.ok, true);
    assert.deepEqual(listed.value.snapshots, [conflictedSnapshot]);
    assert.deepEqual(listed.value.unreadableMaps, []);
  });

  it('hard-fails when listing map files fails', async () => {
    const repository = createRepository({
      listFiles: async () => ({
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: 'Unable to list map files.',
        },
      }),
    });
    const service = new MindMapService(repository);

    const listed = await service.listMaps(true);

    assert.deepEqual(listed, {
      ok: false,
      error: {
        code: 'PERSISTENCE_ERROR',
        message: 'Unable to list map files.',
      },
    });
  });

  it('hard-fails when a map load error is not unreadable-map recovery', async () => {
    const repository = createRepository({
      listFiles: async () => ({
        ok: true,
        value: [
          {
            filePath: 'FocusMaps/broken.json',
            fileName: 'broken.json',
          },
        ],
      }),
      loadMap: async () => ({
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: 'GitHub request failed.',
        },
      }),
    });
    const service = new MindMapService(repository);

    const listed = await service.listMaps(true);

    assert.deepEqual(listed, {
      ok: false,
      error: {
        code: 'PERSISTENCE_ERROR',
        message: 'GitHub request failed.',
      },
    });
  });
});

describe('MindMapService.loadMap', () => {
  it('returns unreadable-map errors for direct loads and clears any stale cache entry', async () => {
    const cachedSnapshot = createSnapshot({
      fileName: 'broken.json',
      mapName: 'Broken',
      updatedAt: '2026-04-09T11:00:00Z',
    });
    const repository = createRepository({
      loadMap: async () => ({
        ok: false,
        error: {
          code: 'UNREADABLE_MAP',
          filePath: cachedSnapshot.filePath,
          fileName: cachedSnapshot.fileName,
          mapName: cachedSnapshot.mapName,
          revision: 'rev-broken',
          reason: 'invalidJson',
          message: 'Map "broken.json" is not valid JSON and cannot be loaded.',
          rawText: '{"rootNode": ',
        },
      }),
    });
    const service = new MindMapService(repository);
    service.hydrateSnapshots([cachedSnapshot]);

    const loaded = await service.loadMap(cachedSnapshot.filePath, true);

    assert.equal(loaded.ok, false);
    assert.equal(loaded.error.code, 'UNREADABLE_MAP');
    assert.equal(loaded.error.reason, 'invalidJson');
    assert.equal(service.getCachedSnapshot(cachedSnapshot.filePath), null);
  });
});

describe('MindMapService cached snapshots', () => {
  it('can replace and remove cached snapshots without rebuilding the whole cache', () => {
    const service = new MindMapService(createRepository());
    const originalSnapshot = createSnapshot({
      fileName: 'repairable.json',
      mapName: 'Repairable',
      updatedAt: '2026-04-09T11:00:00Z',
    });
    const repairedSnapshot = {
      ...originalSnapshot,
      revision: 'rev-2',
      loadedAt: 123,
    };

    service.hydrateSnapshots([originalSnapshot]);
    service.replaceCachedSnapshot(repairedSnapshot);

    assert.deepEqual(service.getCachedSnapshots(), [repairedSnapshot]);

    service.removeCachedSnapshot(repairedSnapshot.filePath);

    assert.deepEqual(service.getCachedSnapshots(), []);
  });
});

describe('MindMapService attachment APIs', () => {
  it('passes nodeId through to loadAttachment', async () => {
    const calls = [];
    const service = new MindMapService({
      async loadAttachment(mapFilePath, nodeId, relativePath, mediaType) {
        calls.push([mapFilePath, nodeId, relativePath, mediaType]);
        return { ok: true, value: new Blob(['data'], { type: mediaType }) };
      },
    });

    const result = await service.loadAttachment(
      'FocusMaps/Alpha.json',
      '53ba90f9-f653-4771-bc08-3c8a531b9b85',
      'note.png',
      'image/png',
    );

    assert.equal(result.ok, true);
    assert.deepEqual(calls, [[
      'FocusMaps/Alpha.json',
      '53ba90f9-f653-4771-bc08-3c8a531b9b85',
      'note.png',
      'image/png',
    ]]);
  });
});
