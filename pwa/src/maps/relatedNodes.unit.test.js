import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { normalizeMindMapDocument } from './model.js';
import {
  collectBacklinkRelatedNodeEntries,
  collectOutgoingRelatedNodeEntries,
  formatBacklinkRelationLabel,
  formatOutgoingRelationLabel,
} from './relatedNodes.js';

const IDS = Object.freeze({
  alphaRoot: '11111111-1111-4111-8111-111111111111',
  alphaBranch: '11111111-1111-4111-8111-111111111112',
  betaRoot: '22222222-2222-4222-8222-222222222221',
  betaTask: '22222222-2222-4222-8222-222222222222',
  gammaRoot: '33333333-3333-4333-8333-333333333331',
  gammaBranch: '33333333-3333-4333-8333-333333333332',
  missingTarget: '44444444-4444-4444-8444-444444444444',
});

function createSnapshot(fileName, rootNode) {
  return {
    filePath: `FocusMaps/${fileName}`,
    fileName,
    mapName: fileName.replace(/\.json$/i, ''),
    document: normalizeMindMapDocument({
      rootNode,
    }, {
      fileTimestampIso: '2026-04-22T08:00:00Z',
    }),
    revision: 'rev-1',
    loadedAt: 0,
  };
}

function createNode({
  uniqueIdentifier,
  name,
  children = [],
  links = {},
}) {
  return {
    nodeType: 0,
    uniqueIdentifier,
    name,
    children,
    links,
    number: 1,
    collapsed: false,
    hideDoneTasks: false,
    taskState: 0,
    metadata: {
      createdAtUtc: '2026-04-22T08:00:00Z',
      updatedAtUtc: '2026-04-22T08:00:00Z',
      source: 'manual',
      device: 'focus-pwa-web',
      attachments: [],
    },
  };
}

describe('relatedNodes relation labels', () => {
  it('formats outgoing and backlink labels with console-style relation names', () => {
    assert.equal(formatOutgoingRelationLabel(0), 'relates');
    assert.equal(formatOutgoingRelationLabel(1), 'prerequisite');
    assert.equal(formatOutgoingRelationLabel(2), 'todo-with');
    assert.equal(formatOutgoingRelationLabel(3), 'causes');
    assert.equal(formatOutgoingRelationLabel(99), 'link');

    assert.equal(formatBacklinkRelationLabel(1), 'backlink: prerequisite');
    assert.equal(formatBacklinkRelationLabel(99), 'backlink');
  });
});

describe('collectOutgoingRelatedNodeEntries', () => {
  it('resolves outgoing links across loaded maps, omits missing targets, and sorts by map then path', () => {
    const alphaSnapshot = createSnapshot('Alpha.json', createNode({
      uniqueIdentifier: IDS.alphaRoot,
      name: 'Alpha',
      children: [
        createNode({
          uniqueIdentifier: IDS.alphaBranch,
          name: 'Alpha Branch',
          links: {
            [IDS.gammaBranch]: { id: IDS.gammaBranch, relationType: 0, metadata: null },
            [IDS.betaTask]: { id: IDS.betaTask, relationType: 1, metadata: null },
            [IDS.missingTarget]: { id: IDS.missingTarget, relationType: 3, metadata: null },
          },
        }),
      ],
    }));
    const betaSnapshot = createSnapshot('Beta.json', createNode({
      uniqueIdentifier: IDS.betaRoot,
      name: 'Beta',
      children: [
        createNode({
          uniqueIdentifier: IDS.betaTask,
          name: 'Beta Task',
        }),
      ],
    }));
    const gammaSnapshot = createSnapshot('Gamma.json', createNode({
      uniqueIdentifier: IDS.gammaRoot,
      name: 'Gamma',
      children: [
        createNode({
          uniqueIdentifier: IDS.gammaBranch,
          name: 'Gamma Branch',
        }),
      ],
    }));

    const sourceNode = alphaSnapshot.document.rootNode.children[0];
    const entries = collectOutgoingRelatedNodeEntries(sourceNode, [
      gammaSnapshot,
      alphaSnapshot,
      betaSnapshot,
    ]);

    assert.deepEqual(entries, [
      {
        direction: 'outgoing',
        mapPath: 'FocusMaps/Beta.json',
        mapName: 'Beta',
        nodeId: IDS.betaTask,
        nodeName: 'Beta Task',
        nodePath: 'Beta > Beta Task',
        nodePathSegments: ['Beta', 'Beta Task'],
        relationLabel: 'prerequisite',
      },
      {
        direction: 'outgoing',
        mapPath: 'FocusMaps/Gamma.json',
        mapName: 'Gamma',
        nodeId: IDS.gammaBranch,
        nodeName: 'Gamma Branch',
        nodePath: 'Gamma > Gamma Branch',
        nodePathSegments: ['Gamma', 'Gamma Branch'],
        relationLabel: 'relates',
      },
    ]);
  });
});

describe('collectBacklinkRelatedNodeEntries', () => {
  it('collects backlinks across loaded maps with map/path context and backlink labels', () => {
    const alphaSnapshot = createSnapshot('Alpha.json', createNode({
      uniqueIdentifier: IDS.alphaRoot,
      name: 'Alpha',
      children: [
        createNode({
          uniqueIdentifier: IDS.alphaBranch,
          name: 'Alpha Branch',
          links: {
            [IDS.betaTask]: { id: IDS.betaTask, relationType: 3, metadata: null },
          },
        }),
      ],
    }));
    const betaSnapshot = createSnapshot('Beta.json', createNode({
      uniqueIdentifier: IDS.betaRoot,
      name: 'Beta',
      children: [
        createNode({
          uniqueIdentifier: IDS.betaTask,
          name: 'Beta Task',
        }),
      ],
    }));
    const gammaSnapshot = createSnapshot('Gamma.json', createNode({
      uniqueIdentifier: IDS.gammaRoot,
      name: 'Gamma',
      children: [
        createNode({
          uniqueIdentifier: IDS.gammaBranch,
          name: 'Gamma Branch',
          links: {
            [IDS.betaTask]: { id: IDS.betaTask, relationType: 0, metadata: null },
          },
        }),
      ],
    }));

    const entries = collectBacklinkRelatedNodeEntries(IDS.betaTask, [
      betaSnapshot,
      gammaSnapshot,
      alphaSnapshot,
    ]);

    assert.deepEqual(entries, [
      {
        direction: 'backlink',
        mapPath: 'FocusMaps/Alpha.json',
        mapName: 'Alpha',
        nodeId: IDS.alphaBranch,
        nodeName: 'Alpha Branch',
        nodePath: 'Alpha > Alpha Branch',
        nodePathSegments: ['Alpha', 'Alpha Branch'],
        relationLabel: 'backlink: causes',
      },
      {
        direction: 'backlink',
        mapPath: 'FocusMaps/Gamma.json',
        mapName: 'Gamma',
        nodeId: IDS.gammaBranch,
        nodeName: 'Gamma Branch',
        nodePath: 'Gamma > Gamma Branch',
        nodePathSegments: ['Gamma', 'Gamma Branch'],
        relationLabel: 'backlink: relates',
      },
    ]);
  });
});
