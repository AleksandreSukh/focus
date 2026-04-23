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

function createAttachment({ id, relativePath, displayName }) {
  return {
    id,
    relativePath,
    mediaType: 'image/png',
    displayName,
    createdAtUtc: '2026-04-20T08:00:00Z',
  };
}

function createNode({ id, name, number = 1, attachments = [], children = [] }) {
  return {
    nodeType: 0,
    uniqueIdentifier: id,
    name,
    children,
    links: {},
    number,
    collapsed: false,
    hideDoneTasks: false,
    taskState: 0,
    metadata: {
      createdAtUtc: '2026-04-20T08:00:00Z',
      updatedAtUtc: '2026-04-20T08:00:00Z',
      source: 'manual',
      device: 'focus-pwa-web',
      attachments,
    },
  };
}

function createDeleteNodeSnapshot({
  revision = 'rev-1',
  updatedAt = '2026-04-20T08:00:00Z',
} = {}) {
  const snapshot = createSnapshot({
    fileName: 'delete-node.json',
    mapName: 'Delete Node',
    updatedAt,
  });
  const branchId = '53ba90f9-f653-4771-bc08-3c8a531b9b85';
  const leafId = 'b01e3fc3-67d7-46b0-b7f0-2f8bd3c5ca1a';
  const siblingId = 'fe9c1c3d-b070-4d60-a51a-572f2c653eb7';

  snapshot.document.rootNode.children = [
    createNode({
      id: branchId,
      name: 'Branch',
      number: 1,
      attachments: [
        createAttachment({
          id: '11111111-1111-4111-8111-111111111111',
          relativePath: 'branch.png',
          displayName: 'Branch image',
        }),
      ],
      children: [
        createNode({
          id: leafId,
          name: 'Leaf',
          number: 1,
          attachments: [
            createAttachment({
              id: '22222222-2222-4222-8222-222222222222',
              relativePath: 'leaf.png',
              displayName: 'Leaf image',
            }),
          ],
        }),
      ],
    }),
    createNode({
      id: siblingId,
      name: 'Sibling',
      number: 2,
    }),
  ];
  snapshot.revision = revision;

  return {
    snapshot,
    branchId,
    leafId,
    siblingId,
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
    async saveMap(filePath, document, revision, commitMessage) {
      return overrides.saveMap
        ? overrides.saveMap(filePath, document, revision, commitMessage)
        : {
            ok: false,
            error: {
              code: 'NOT_IMPLEMENTED',
              message: `No mock saveMap result for "${filePath}".`,
            },
          };
    },
    async deleteAttachment(mapFilePath, nodeId, relativePath, versionToken, commitMessage) {
      return overrides.deleteAttachment
        ? overrides.deleteAttachment(mapFilePath, nodeId, relativePath, versionToken, commitMessage)
        : {
            ok: false,
            error: {
              code: 'NOT_IMPLEMENTED',
              message: `No mock deleteAttachment result for "${relativePath}".`,
            },
          };
    },
    async renameMap(oldFilePath, newFilePath, document, oldRevision, commitMessage) {
      return overrides.renameMap
        ? overrides.renameMap(oldFilePath, newFilePath, document, oldRevision, commitMessage)
        : {
            ok: false,
            error: {
              code: 'NOT_IMPLEMENTED',
              message: `No mock renameMap result for "${oldFilePath}".`,
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

describe('MindMapService.renameMap', () => {
  it('persists the renamed root node text into the document sent to the repository', async () => {
    const snapshot = createSnapshot({
      fileName: 'Old root.json',
      mapName: 'Old root',
      updatedAt: '2026-04-21T10:00:00Z',
    });
    snapshot.document.rootNode.name = 'Old root';

    const renameCalls = [];
    const service = new MindMapService(createRepository({
      renameMap: async (oldFilePath, newFilePath, document, oldRevision, commitMessage) => {
        renameCalls.push({
          oldFilePath,
          newFilePath,
          oldRevision,
          commitMessage,
          rootName: document.rootNode.name,
          updatedAt: document.updatedAt,
        });
        return {
          ok: true,
          revision: 'rev-2',
        };
      },
    }));
    service.hydrateSnapshots([snapshot]);

    const result = await service.renameMap({
      type: 'renameMap',
      filePath: snapshot.filePath,
      newFilePath: 'FocusMaps/Renamed root.json',
      nodeId: snapshot.document.rootNode.uniqueIdentifier,
      text: 'Renamed root',
      oldRevision: snapshot.revision,
      timestamp: '2026-04-21T11:30:00Z',
      commitMessage: 'map:rename Old root -> Renamed root',
    });

    assert.equal(result.ok, true);
    assert.deepEqual(renameCalls, [
      {
        oldFilePath: 'FocusMaps/Old root.json',
        newFilePath: 'FocusMaps/Renamed root.json',
        oldRevision: 'rev-1',
        commitMessage: 'map:rename Old root -> Renamed root',
        rootName: 'Renamed root',
        updatedAt: '2026-04-21T11:30:00Z',
      },
    ]);
    assert.equal(result.value.snapshot.filePath, 'FocusMaps/Renamed root.json');
    assert.equal(result.value.snapshot.fileName, 'Renamed root.json');
    assert.equal(result.value.snapshot.mapName, 'Renamed root');
    assert.equal(result.value.snapshot.document.rootNode.name, 'Renamed root');
    assert.equal(result.value.snapshot.revision, 'rev-2');
    assert.equal(result.value.mutation.selectedNodeId, snapshot.document.rootNode.uniqueIdentifier);
  });

  it('keeps the renamed root node text in the service cache for later reads', async () => {
    const snapshot = createSnapshot({
      fileName: 'Original.json',
      mapName: 'Original',
      updatedAt: '2026-04-21T10:00:00Z',
    });
    snapshot.document.rootNode.name = 'Original';

    let loadCalls = 0;
    const service = new MindMapService(createRepository({
      loadMap: async () => {
        loadCalls += 1;
        return {
          ok: false,
          error: {
            code: 'NOT_EXPECTED',
            message: 'loadMap should not be called for a cached renamed snapshot.',
          },
        };
      },
      renameMap: async () => ({
        ok: true,
        revision: 'rev-2',
      }),
    }));
    service.hydrateSnapshots([snapshot]);

    const renamed = await service.renameMap({
      type: 'renameMap',
      filePath: snapshot.filePath,
      newFilePath: 'FocusMaps/Renamed.json',
      nodeId: snapshot.document.rootNode.uniqueIdentifier,
      text: 'Renamed',
      oldRevision: snapshot.revision,
      timestamp: '2026-04-21T11:30:00Z',
      commitMessage: 'map:rename Original -> Renamed',
    });

    assert.equal(renamed.ok, true);
    assert.equal(service.getCachedSnapshot(snapshot.filePath), null);

    const cached = service.getCachedSnapshot('FocusMaps/Renamed.json');
    assert.equal(cached.document.rootNode.name, 'Renamed');

    const loaded = await service.loadMap('FocusMaps/Renamed.json', false);
    assert.equal(loaded.ok, true);
    assert.equal(loaded.value.document.rootNode.name, 'Renamed');
    assert.equal(loadCalls, 0);
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

describe('MindMapService.applyMutation deleteNode attachment cleanup', () => {
  it('deletes subtree attachments before saving the map mutation', async () => {
    const { snapshot, branchId, leafId, siblingId } = createDeleteNodeSnapshot();
    const calls = [];
    const repository = createRepository({
      loadMap: async () => ({
        ok: true,
        value: snapshot,
      }),
      deleteAttachment: async (mapFilePath, nodeId, relativePath, versionToken, commitMessage) => {
        calls.push({
          type: 'deleteAttachment',
          mapFilePath,
          nodeId,
          relativePath,
          versionToken,
          commitMessage,
        });
        return { ok: true };
      },
      saveMap: async (filePath, document, revision, commitMessage) => {
        calls.push({
          type: 'saveMap',
          filePath,
          revision,
          commitMessage,
          remainingChildren: document.rootNode.children.map((child) => child.uniqueIdentifier),
        });
        return { ok: true, revision: 'rev-2' };
      },
    });
    const service = new MindMapService(repository);

    const result = await service.applyMutation(
      snapshot.filePath,
      {
        type: 'deleteNode',
        nodeId: branchId,
        timestamp: '2026-04-20T09:15:00Z',
      },
      'map:delete Delete Node branch',
    );

    assert.equal(result.ok, true);
    assert.deepEqual(calls, [
      {
        type: 'deleteAttachment',
        mapFilePath: snapshot.filePath,
        nodeId: branchId,
        relativePath: 'branch.png',
        versionToken: null,
        commitMessage: 'map:delete Delete Node branch',
      },
      {
        type: 'deleteAttachment',
        mapFilePath: snapshot.filePath,
        nodeId: leafId,
        relativePath: 'leaf.png',
        versionToken: null,
        commitMessage: 'map:delete Delete Node branch',
      },
      {
        type: 'saveMap',
        filePath: snapshot.filePath,
        revision: 'rev-1',
        commitMessage: 'map:delete Delete Node branch',
        remainingChildren: [siblingId],
      },
    ]);
    assert.deepEqual(
      result.value.snapshot.document.rootNode.children.map((child) => child.uniqueIdentifier),
      [siblingId],
    );
  });

  it('aborts the save when subtree attachment deletion fails', async () => {
    const { snapshot, branchId } = createDeleteNodeSnapshot();
    let saveCalled = false;
    const repository = createRepository({
      loadMap: async () => ({
        ok: true,
        value: snapshot,
      }),
      deleteAttachment: async (_mapFilePath, _nodeId, relativePath) => {
        if (relativePath === 'leaf.png') {
          return {
            ok: false,
            error: {
              code: 'PERSISTENCE_ERROR',
              message: 'Could not delete leaf attachment.',
              retriable: true,
            },
          };
        }

        return { ok: true };
      },
      saveMap: async () => {
        saveCalled = true;
        return { ok: true, revision: 'rev-2' };
      },
    });
    const service = new MindMapService(repository);

    const result = await service.applyMutation(
      snapshot.filePath,
      {
        type: 'deleteNode',
        nodeId: branchId,
        timestamp: '2026-04-20T09:15:00Z',
      },
      'map:delete Delete Node branch',
    );

    assert.equal(result.ok, false);
    assert.equal(result.error.message, 'Could not delete leaf attachment.');
    assert.equal(saveCalled, false);
  });

  it('retries subtree attachment deletion after a stale-state save conflict', async () => {
    const initial = createDeleteNodeSnapshot({
      revision: 'rev-1',
      updatedAt: '2026-04-20T08:00:00Z',
    });
    const refreshed = createDeleteNodeSnapshot({
      revision: 'rev-2',
      updatedAt: '2026-04-20T08:05:00Z',
    });
    const attachmentDeletes = [];
    let loadCount = 0;
    let saveCount = 0;
    const repository = createRepository({
      loadMap: async () => {
        loadCount += 1;
        return {
          ok: true,
          value: loadCount === 1 ? initial.snapshot : refreshed.snapshot,
        };
      },
      deleteAttachment: async (mapFilePath, nodeId, relativePath, versionToken, commitMessage) => {
        attachmentDeletes.push({
          mapFilePath,
          nodeId,
          relativePath,
          versionToken,
          commitMessage,
        });
        return { ok: true };
      },
      saveMap: async (_filePath, _document, _revision, _commitMessage) => {
        saveCount += 1;
        return saveCount === 1
          ? {
              ok: false,
              error: {
                code: 'STALE_STATE',
                message: 'Map changed remotely.',
                retriable: true,
              },
            }
          : {
              ok: true,
              revision: 'rev-3',
            };
      },
    });
    const service = new MindMapService(repository);

    const result = await service.applyMutation(
      initial.snapshot.filePath,
      {
        type: 'deleteNode',
        nodeId: initial.branchId,
        timestamp: '2026-04-20T09:15:00Z',
      },
      'map:delete Delete Node branch',
    );

    assert.equal(result.ok, true);
    assert.equal(loadCount, 2);
    assert.equal(saveCount, 2);
    assert.deepEqual(
      attachmentDeletes.map((attachment) => [attachment.nodeId, attachment.relativePath]),
      [
        [initial.branchId, 'branch.png'],
        [initial.leafId, 'leaf.png'],
        [refreshed.branchId, 'branch.png'],
        [refreshed.leafId, 'leaf.png'],
      ],
    );
  });
});
