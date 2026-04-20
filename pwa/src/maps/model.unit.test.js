import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  TASK_STATE,
  applyMapMutation,
  getVisibleTreeChildren,
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
});
