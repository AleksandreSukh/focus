import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { normalizeMindMapDocument, TASK_STATE } from './model.js';
import {
  buildHashRoute,
  parseHashRoute,
  resolveViewportNodeId,
  shouldShowWorkspaceTabs,
} from './routes.js';

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
              name: 'Open child',
              taskState: TASK_STATE.TODO,
              children: [],
            },
            {
              name: 'Done child',
              taskState: TASK_STATE.DONE,
              children: [],
            },
          ],
        },
      ],
    },
  }, {
    fileTimestampIso: '2026-04-21T08:00:00Z',
  });
}

describe('map subtree hash routes', () => {
  it('builds root map routes without a node query', () => {
    assert.equal(
      buildHashRoute('map', 'maps/alpha.json', 'root-node', 'root-node'),
      '#map/maps%2Falpha.json',
    );
  });

  it('builds subtree routes with a node query', () => {
    assert.equal(
      buildHashRoute('map', 'maps/alpha.json', 'branch-node', 'root-node'),
      '#map/maps%2Falpha.json?node=branch-node',
    );
  });

  it('parses subtree map routes with node ids', () => {
    assert.deepEqual(
      parseHashRoute('#map/maps%2Falpha.json?node=branch-node'),
      {
        view: 'map',
        mapPath: 'maps/alpha.json',
        nodeId: 'branch-node',
        isInvalid: false,
      },
    );
  });
});

describe('map subtree view state', () => {
  it('falls back to the nearest visible ancestor for hidden done nodes', () => {
    const document = createDocument();
    const branchId = document.rootNode.children[0].uniqueIdentifier;
    const hiddenDoneChildId = document.rootNode.children[0].children[1].uniqueIdentifier;

    assert.equal(resolveViewportNodeId(document, hiddenDoneChildId), branchId);
  });

  it('shows workspace tabs at the document root and hides them for subtree views', () => {
    const document = createDocument();
    const rootId = document.rootNode.uniqueIdentifier;
    const branchId = document.rootNode.children[0].uniqueIdentifier;

    assert.equal(shouldShowWorkspaceTabs('map', document, rootId), true);
    assert.equal(shouldShowWorkspaceTabs('map', document, branchId), false);
    assert.equal(shouldShowWorkspaceTabs('tasks', document, branchId), true);
  });
});
