import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  TASK_STATE,
  applyMapMutation,
  getVisibleTreeChildren,
  hasDoneDescendants,
  hasHideDoneAncestor,
  normalizeMindMapDocument,
  resolveVisibleNodeId,
} from './model.js';

function createDocument() {
  return normalizeMindMapDocument({
    rootNode: {
      name: 'Root',
      children: [
        {
          name: 'Branch',
          hideDoneTasks: true,
          children: [
            {
              name: 'Visible child',
              taskState: TASK_STATE.TODO,
              children: [],
            },
            {
              name: 'Hidden done child',
              taskState: TASK_STATE.DONE,
              children: [],
            },
          ],
        },
      ],
    },
  }, {
    fileTimestampIso: '2026-04-20T08:00:00Z',
  });
}

describe('mind map model hide-done support', () => {
  it('normalizes legacy HideDoneTasks casing to hideDoneTasks', () => {
    const document = normalizeMindMapDocument({
      RootNode: {
        Name: 'Root',
        Children: [
          {
            Name: 'Branch',
            HideDoneTasks: true,
            Children: [],
          },
        ],
      },
    }, {
      fileTimestampIso: '2026-04-20T08:00:00Z',
    });

    assert.equal(document.rootNode.children[0].hideDoneTasks, true);
    assert.equal('HideDoneTasks' in document.rootNode.children[0], false);
  });

  it('applies setHideDoneTasks as a persisted node mutation', () => {
    const document = createDocument();
    const branchId = document.rootNode.children[0].uniqueIdentifier;

    const result = applyMapMutation(document, {
      type: 'setHideDoneTasks',
      nodeId: branchId,
      hideDoneTasks: false,
      timestamp: '2026-04-20T09:15:00Z',
    });

    assert.equal(result.ok, true);
    assert.equal(document.rootNode.children[0].hideDoneTasks, false);
    assert.equal(document.updatedAt, '2026-04-20T09:15:00Z');
  });

  it('filters done descendants from tree rendering while keeping open descendants visible', () => {
    const document = createDocument();
    const [branch] = document.rootNode.children;

    const visibleChildren = getVisibleTreeChildren(branch, false);

    assert.deepEqual(
      visibleChildren.map((child) => child.name),
      ['Visible child'],
    );
  });

  it('resolves hidden task selections to the nearest visible ancestor', () => {
    const document = createDocument();
    const branchId = document.rootNode.children[0].uniqueIdentifier;
    const hiddenDoneChildId = document.rootNode.children[0].children[1].uniqueIdentifier;

    assert.equal(resolveVisibleNodeId(document, hiddenDoneChildId), branchId);
  });

  it('detects hide-done ancestors for subtree rendering', () => {
    const document = createDocument();
    const openChildId = document.rootNode.children[0].children[0].uniqueIdentifier;

    assert.equal(hasHideDoneAncestor(document, openChildId), true);
  });

  it('returns false when a node has no descendants', () => {
    const document = normalizeMindMapDocument({
      rootNode: {
        name: 'Root',
        children: [],
      },
    }, {
      fileTimestampIso: '2026-04-20T08:00:00Z',
    });

    assert.equal(hasDoneDescendants(document, document.rootNode.uniqueIdentifier), false);
  });

  it('returns false when descendants are open only', () => {
    const document = normalizeMindMapDocument({
      rootNode: {
        name: 'Root',
        children: [
          {
            name: 'Todo child',
            taskState: TASK_STATE.TODO,
            children: [],
          },
          {
            name: 'Doing child',
            taskState: TASK_STATE.DOING,
            children: [],
          },
        ],
      },
    }, {
      fileTimestampIso: '2026-04-20T08:00:00Z',
    });

    assert.equal(hasDoneDescendants(document, document.rootNode.uniqueIdentifier), false);
  });

  it('returns true when a node has a done descendant', () => {
    const document = normalizeMindMapDocument({
      rootNode: {
        name: 'Root',
        children: [
          {
            name: 'Done child',
            taskState: TASK_STATE.DONE,
            children: [],
          },
        ],
      },
    }, {
      fileTimestampIso: '2026-04-20T08:00:00Z',
    });

    assert.equal(hasDoneDescendants(document, document.rootNode.uniqueIdentifier), true);
  });

  it('ignores the selected node task state when checking descendants', () => {
    const document = normalizeMindMapDocument({
      rootNode: {
        name: 'Root',
        taskState: TASK_STATE.DONE,
        children: [],
      },
    }, {
      fileTimestampIso: '2026-04-20T08:00:00Z',
    });

    assert.equal(hasDoneDescendants(document, document.rootNode.uniqueIdentifier), false);
  });

  it('detects nested done descendants recursively', () => {
    const document = normalizeMindMapDocument({
      rootNode: {
        name: 'Root',
        children: [
          {
            name: 'Branch',
            taskState: TASK_STATE.TODO,
            children: [
              {
                name: 'Nested done',
                taskState: TASK_STATE.DONE,
                children: [],
              },
            ],
          },
        ],
      },
    }, {
      fileTimestampIso: '2026-04-20T08:00:00Z',
    });
    const branchId = document.rootNode.children[0].uniqueIdentifier;

    assert.equal(hasDoneDescendants(document, branchId), true);
  });
});

describe('mind map model delete-node attachment cleanup metadata', () => {
  it('returns deleted subtree attachments and keeps parent selection after node removal', () => {
    const document = normalizeMindMapDocument({
      rootNode: {
        name: 'Root',
        children: [
          {
            name: 'Branch',
            metadata: {
              attachments: [
                {
                  id: '11111111-1111-4111-8111-111111111111',
                  relativePath: 'branch.png',
                  mediaType: 'image/png',
                  displayName: 'Branch image',
                  createdAtUtc: '2026-04-20T08:00:00Z',
                },
              ],
            },
            children: [
              {
                name: 'Leaf',
                metadata: {
                  attachments: [
                    {
                      id: '22222222-2222-4222-8222-222222222222',
                      relativePath: 'leaf.png',
                      mediaType: 'image/png',
                      displayName: 'Leaf image',
                      createdAtUtc: '2026-04-20T08:00:00Z',
                    },
                  ],
                },
                children: [],
              },
            ],
          },
          {
            name: 'Sibling',
            children: [],
          },
        ],
      },
    }, {
      fileTimestampIso: '2026-04-20T08:00:00Z',
    });
    const branch = document.rootNode.children[0];
    const leaf = branch.children[0];
    const sibling = document.rootNode.children[1];

    const result = applyMapMutation(document, {
      type: 'deleteNode',
      nodeId: branch.uniqueIdentifier,
      timestamp: '2026-04-20T09:15:00Z',
    });

    assert.equal(result.ok, true);
    assert.equal(result.value.selectedNodeId, document.rootNode.uniqueIdentifier);
    assert.deepEqual(
      result.value.deletedAttachments.map((attachment) => ({
        nodeId: attachment.nodeId,
        relativePath: attachment.relativePath,
      })),
      [
        {
          nodeId: branch.uniqueIdentifier,
          relativePath: 'branch.png',
        },
        {
          nodeId: leaf.uniqueIdentifier,
          relativePath: 'leaf.png',
        },
      ],
    );
    assert.deepEqual(
      document.rootNode.children.map((child) => child.uniqueIdentifier),
      [sibling.uniqueIdentifier],
    );
  });
});
