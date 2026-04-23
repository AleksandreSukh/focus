import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { hasConflictMarkers, tryResolveMapConflict } from './mapConflictResolver.js';

function createDocument(overrides = {}) {
  const rootNode = overrides.rootNode ?? {
    nodeType: 0,
    uniqueIdentifier: 'root-0000-0000-4000-8000-000000000001',
    name: 'Root',
    children: [],
    links: {},
    number: 1,
    collapsed: false,
    hideDoneTasks: false,
    taskState: 0,
    metadata: {
      createdAtUtc: '2026-04-20T08:00:00Z',
      updatedAtUtc: '2026-04-20T08:00:00Z',
      source: 'manual',
      device: 'focus-pwa-web',
      attachments: [],
    },
  };

  return {
    updatedAt: overrides.updatedAt ?? '2026-04-20T08:00:00Z',
    rootNode,
    ...Object.fromEntries(Object.entries(overrides).filter(([key]) => key !== 'rootNode' && key !== 'updatedAt')),
  };
}

function createConflict(ours, theirs) {
  return [
    '<<<<<<< HEAD',
    JSON.stringify(ours, null, 2),
    '=======',
    JSON.stringify(theirs, null, 2),
    '>>>>>>> main',
  ].join('\n');
}

describe('mapConflictResolver', () => {
  it('detects raw Git conflict markers', () => {
    assert.equal(hasConflictMarkers('<<<<<<< HEAD\nleft\n=======\nright\n>>>>>>> main\n'), true);
    assert.equal(hasConflictMarkers('{"rootNode":{}}'), false);
  });

  it('resolves simple marker conflicts and keeps newer scalar node fields', () => {
    const ours = createDocument({
      updatedAt: '2026-04-20T08:00:00Z',
      rootNode: {
        nodeType: 0,
        uniqueIdentifier: 'root-0000-0000-4000-8000-000000000001',
        name: 'Older name',
        children: [],
        links: {},
        number: 1,
        collapsed: false,
        hideDoneTasks: false,
        taskState: 1,
        metadata: {
          createdAtUtc: '2026-04-20T08:00:00Z',
          updatedAtUtc: '2026-04-20T08:00:00Z',
          source: 'manual',
          device: 'focus-pwa-web',
          attachments: [],
        },
      },
    });
    const theirs = createDocument({
      updatedAt: '2026-04-20T08:05:00Z',
      rootNode: {
        ...ours.rootNode,
        name: 'Newer name',
        taskState: 3,
        metadata: {
          ...ours.rootNode.metadata,
          updatedAtUtc: '2026-04-20T08:05:00Z',
        },
      },
    });

    const resolved = tryResolveMapConflict(createConflict(ours, theirs));

    assert.equal(resolved.ok, true);
    const document = JSON.parse(resolved.resolvedContent);
    assert.equal(document.rootNode.name, 'Newer name');
    assert.equal(document.rootNode.taskState, 3);
  });

  it('keeps newer hide-done explicit markers on node merges', () => {
    const ours = createDocument({
      updatedAt: '2026-04-20T08:00:00Z',
    });
    const theirs = createDocument({
      updatedAt: '2026-04-20T08:05:00Z',
      rootNode: {
        ...ours.rootNode,
        hideDoneTasks: false,
        hideDoneTasksExplicit: true,
        metadata: {
          ...ours.rootNode.metadata,
          updatedAtUtc: '2026-04-20T08:05:00Z',
        },
      },
    });

    const resolved = tryResolveMapConflict(createConflict(ours, theirs));

    assert.equal(resolved.ok, true);
    const document = JSON.parse(resolved.resolvedContent);
    assert.equal(document.rootNode.hideDoneTasksExplicit, true);
  });

  it('unions children, links, and attachments by stable identifiers', () => {
    const sharedRoot = {
      nodeType: 0,
      uniqueIdentifier: 'root-0000-0000-4000-8000-000000000001',
      name: 'Root',
      children: [
        {
          nodeType: 0,
          uniqueIdentifier: 'child-0000-0000-4000-8000-000000000001',
          name: 'Left child',
          children: [],
          links: {},
          number: 1,
          collapsed: false,
          hideDoneTasks: false,
          taskState: 0,
          metadata: {
            createdAtUtc: '2026-04-20T08:00:00Z',
            updatedAtUtc: '2026-04-20T08:00:00Z',
            source: 'manual',
            device: 'focus-pwa-web',
            attachments: [],
          },
        },
      ],
      links: {
        'node-a': {
          id: 'link-a',
          relationType: 1,
          metadata: {},
        },
      },
      number: 1,
      collapsed: false,
      hideDoneTasks: false,
      taskState: 0,
      metadata: {
        createdAtUtc: '2026-04-20T08:00:00Z',
        updatedAtUtc: '2026-04-20T08:00:00Z',
        source: 'manual',
        device: 'focus-pwa-web',
        attachments: [
          {
            id: 'att-left',
            relativePath: 'left.png',
            mediaType: 'image/png',
            displayName: 'left',
            createdAtUtc: '2026-04-20T08:00:00Z',
          },
        ],
      },
    };
    const ours = createDocument({ rootNode: sharedRoot });
    const theirs = createDocument({
      rootNode: {
        ...sharedRoot,
        children: [
          ...sharedRoot.children,
          {
            nodeType: 0,
            uniqueIdentifier: 'child-0000-0000-4000-8000-000000000002',
            name: 'Right child',
            children: [],
            links: {},
            number: 1,
            collapsed: false,
            hideDoneTasks: false,
            taskState: 0,
            metadata: {
              createdAtUtc: '2026-04-20T08:01:00Z',
              updatedAtUtc: '2026-04-20T08:01:00Z',
              source: 'manual',
              device: 'focus-pwa-web',
              attachments: [],
            },
          },
        ],
        links: {
          ...sharedRoot.links,
          'node-b': {
            id: 'link-b',
            relationType: 1,
            metadata: {},
          },
        },
        metadata: {
          ...sharedRoot.metadata,
          attachments: [
            ...sharedRoot.metadata.attachments,
            {
              id: 'att-right',
              relativePath: 'right.png',
              mediaType: 'image/png',
              displayName: 'right',
              createdAtUtc: '2026-04-20T08:01:00Z',
            },
          ],
        },
      },
    });

    const resolved = tryResolveMapConflict(createConflict(ours, theirs));

    assert.equal(resolved.ok, true);
    const document = JSON.parse(resolved.resolvedContent);
    assert.deepEqual(
      document.rootNode.children.map((child) => child.uniqueIdentifier),
      [
        'child-0000-0000-4000-8000-000000000001',
        'child-0000-0000-4000-8000-000000000002',
      ],
    );
    assert.deepEqual(Object.keys(document.rootNode.links).sort(), ['node-a', 'node-b']);
    assert.deepEqual(
      document.rootNode.metadata.attachments.map((attachment) => attachment.id).sort(),
      ['att-left', 'att-right'],
    );
    assert.deepEqual(
      document.rootNode.children.map((child) => child.number),
      [1, 2],
    );
  });

  it('falls back to the newer document timestamp when field-level merge cannot classify a difference', () => {
    const ours = createDocument({
      updatedAt: '2026-04-20T08:00:00Z',
      customPreference: 'left',
    });
    const theirs = createDocument({
      updatedAt: '2026-04-20T09:00:00Z',
      customPreference: 'right',
    });

    const resolved = tryResolveMapConflict(createConflict(ours, theirs));

    assert.equal(resolved.ok, true);
    const document = JSON.parse(resolved.resolvedContent);
    assert.equal(document.customPreference, 'right');
    assert.equal(document.updatedAt, '2026-04-20T09:00:00Z');
  });

  it('fails instead of guessing when it cannot classify a conflict and no timestamps are available', () => {
    const ours = {
      rootNode: {
        uniqueIdentifier: 'root-0000-0000-4000-8000-000000000001',
      },
      customPreference: 'left',
    };
    const theirs = {
      rootNode: {
        uniqueIdentifier: 'root-0000-0000-4000-8000-000000000001',
      },
      customPreference: 'right',
    };

    const resolved = tryResolveMapConflict(createConflict(ours, theirs));

    assert.equal(resolved.ok, false);
    assert.equal(resolved.resolvedContent, null);
  });
});
