import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  TASK_STATE,
  createMapDocument,
  normalizeMindMapDocument,
} from './model.js';
import {
  applyPendingOperationsLocally,
  buildCreateMapOperation,
  buildDeleteMapOperation,
  canUseCachedWorkspace,
} from './offlineQueue.js';

function createSnapshot(filePath, mapName) {
  const fileName = filePath.split('/').pop() || filePath;
  return {
    filePath,
    fileName,
    mapName,
    document: createMapDocument(mapName),
    revision: 'rev-1',
    loadedAt: 0,
  };
}

describe('offline map operation queue', () => {
  it('hydrates a locally created map and applies a later edit by stable root id', () => {
    const create = buildCreateMapOperation({
      filePath: 'FocusMaps/New Map.json',
      mapName: 'New Map',
      commitMessage: 'map:create New Map',
      timestamp: '2026-06-11T08:00:00Z',
    });
    const rootId = create.document.rootNode.uniqueIdentifier;

    const snapshots = applyPendingOperationsLocally([], [
      create,
      {
        type: 'editNodeText',
        filePath: create.filePath,
        nodeId: rootId,
        text: 'Renamed while offline',
        timestamp: '2026-06-11T08:01:00Z',
        commitMessage: 'node:edit root',
      },
    ], 123);

    assert.equal(snapshots.length, 1);
    assert.equal(snapshots[0].filePath, create.filePath);
    assert.equal(snapshots[0].document.rootNode.uniqueIdentifier, rootId);
    assert.equal(snapshots[0].document.rootNode.name, 'Renamed while offline');
    assert.equal(snapshots[0].loadedAt, 123);
  });

  it('applies queued operations in FIFO order', () => {
    const snapshot = createSnapshot('FocusMaps/Inbox.json', 'Inbox');
    const rootId = snapshot.document.rootNode.uniqueIdentifier;

    const snapshots = applyPendingOperationsLocally([snapshot], [
      {
        type: 'editNodeText',
        filePath: snapshot.filePath,
        nodeId: rootId,
        text: 'First edit',
        timestamp: '2026-06-11T08:00:00Z',
      },
      {
        type: 'editNodeText',
        filePath: snapshot.filePath,
        nodeId: rootId,
        text: 'Second edit',
        timestamp: '2026-06-11T08:01:00Z',
      },
    ]);

    assert.equal(snapshots[0].document.rootNode.name, 'Second edit');
  });

  it('hydrates rename operations before later operations on the new path', () => {
    const snapshot = createSnapshot('FocusMaps/Old.json', 'Old');
    const rootId = snapshot.document.rootNode.uniqueIdentifier;

    const snapshots = applyPendingOperationsLocally([snapshot], [
      {
        type: 'renameMap',
        filePath: snapshot.filePath,
        newFilePath: 'FocusMaps/New.json',
        oldRevision: snapshot.revision,
        nodeId: rootId,
        text: 'New',
        timestamp: '2026-06-11T08:00:00Z',
      },
      {
        type: 'addChildTask',
        filePath: 'FocusMaps/New.json',
        parentNodeId: rootId,
        newNodeId: '11111111-1111-4111-8111-111111111111',
        text: 'Queued task',
        timestamp: '2026-06-11T08:01:00Z',
      },
    ]);

    assert.equal(snapshots.length, 1);
    assert.equal(snapshots[0].filePath, 'FocusMaps/New.json');
    assert.equal(snapshots[0].document.rootNode.name, 'New');
    assert.equal(snapshots[0].document.rootNode.children[0].name, 'Queued task');
    assert.equal(snapshots[0].document.rootNode.children[0].taskState, TASK_STATE.TODO);
  });

  it('removes a map after a queued delete operation', () => {
    const snapshot = createSnapshot('FocusMaps/Delete Me.json', 'Delete Me');
    const deleteMap = buildDeleteMapOperation({
      snapshot,
      commitMessage: 'map:delete Delete Me',
      timestamp: '2026-06-11T08:00:00Z',
    });

    const snapshots = applyPendingOperationsLocally([snapshot], [deleteMap]);

    assert.deepEqual(snapshots, []);
  });

  it('allows cached offline startup only for transient failures with local state', () => {
    assert.equal(
      canUseCachedWorkspace({ code: 'NETWORK' }, [createSnapshot('FocusMaps/A.json', 'A')], []),
      true,
    );
    assert.equal(
      canUseCachedWorkspace({ code: 'FORBIDDEN' }, [createSnapshot('FocusMaps/A.json', 'A')], []),
      false,
    );
    assert.equal(
      canUseCachedWorkspace({ code: 'NETWORK' }, [], []),
      false,
    );
    assert.equal(
      canUseCachedWorkspace({ code: 'NETWORK' }, [], [
        buildCreateMapOperation({
          filePath: 'FocusMaps/Queued.json',
          mapName: 'Queued',
          commitMessage: 'map:create Queued',
        }),
      ]),
      true,
    );
  });

  it('does not mutate input snapshots while hydrating pending operations', () => {
    const snapshot = createSnapshot('FocusMaps/Immutable.json', 'Immutable');
    normalizeMindMapDocument(snapshot.document);
    const originalDocument = JSON.parse(JSON.stringify(snapshot.document));
    const rootId = snapshot.document.rootNode.uniqueIdentifier;

    applyPendingOperationsLocally([snapshot], [{
      type: 'editNodeText',
      filePath: snapshot.filePath,
      nodeId: rootId,
      text: 'Changed copy',
      timestamp: '2026-06-11T08:00:00Z',
    }]);

    assert.deepEqual(snapshot.document, originalDocument);
  });
});
