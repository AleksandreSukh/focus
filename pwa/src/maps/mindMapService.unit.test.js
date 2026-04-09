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
