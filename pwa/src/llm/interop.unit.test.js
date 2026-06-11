import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { normalizeMindMapDocument, TASK_STATE } from '../maps/model.js';
import {
  LLM_PROMPT_PREFIX,
  applyLlmJobCompletion,
  buildLlmContext,
  collectLlmPromptEntries,
  createLlmJob,
  extractLlmPromptText,
  formatLlmContextMarkdown,
  isLlmPromptNode,
} from './interop.js';

const IDS = {
  root: '11111111-1111-4111-8111-111111111111',
  prompt: '22222222-2222-4222-8222-222222222222',
  child: '33333333-3333-4333-8333-333333333333',
  linked: '44444444-4444-4444-8444-444444444444',
  backlink: '55555555-5555-4555-8555-555555555555',
  answer: '66666666-6666-4666-8666-666666666666',
};

function createSnapshot(fileName, rootNode) {
  return {
    filePath: `FocusMaps/${fileName}`,
    fileName,
    mapName: fileName.replace(/\.json$/i, ''),
    document: normalizeMindMapDocument({
      updatedAt: '2026-05-18T08:00:00Z',
      rootNode,
    }),
    revision: 'rev',
    loadedAt: 1,
  };
}

function createNode(overrides = {}) {
  return {
    nodeType: 0,
    uniqueIdentifier: overrides.uniqueIdentifier,
    name: overrides.name || 'Node',
    children: overrides.children || [],
    links: overrides.links || {},
    number: overrides.number || 1,
    collapsed: false,
    hideDoneTasks: false,
    starred: false,
    taskState: overrides.taskState ?? TASK_STATE.NONE,
    metadata: {
      createdAtUtc: '2026-05-18T08:00:00Z',
      updatedAtUtc: overrides.updatedAtUtc || '2026-05-18T08:00:00Z',
      source: 'manual',
      device: 'test',
      attachments: [],
    },
  };
}

describe('LLM interop prompt detection', () => {
  it('detects open @ai task nodes and extracts prompt text', () => {
    const node = createNode({
      uniqueIdentifier: IDS.prompt,
      name: `${LLM_PROMPT_PREFIX}Summarize this branch`,
      taskState: TASK_STATE.TODO,
    });

    assert.equal(isLlmPromptNode(node), true);
    assert.equal(extractLlmPromptText(node.name), 'Summarize this branch');
  });

  it('collects prompt entries not already covered by job sidecars', () => {
    const snapshot = createSnapshot('Alpha.json', createNode({
      uniqueIdentifier: IDS.root,
      name: 'Alpha',
      children: [
        createNode({
          uniqueIdentifier: IDS.prompt,
          name: '@ai Draft next actions',
          taskState: TASK_STATE.TODO,
        }),
      ],
    }));

    assert.equal(collectLlmPromptEntries([snapshot], []).length, 1);
    assert.equal(
      collectLlmPromptEntries([snapshot], [createLlmJob({
        mapFilePath: snapshot.filePath,
        nodeId: IDS.prompt,
        prompt: 'Draft next actions',
      })]).length,
      0,
    );
  });
});

describe('LLM interop context', () => {
  it('builds deterministic subtree, link, backlink, and URL context', () => {
    const alpha = createSnapshot('Alpha.json', createNode({
      uniqueIdentifier: IDS.root,
      name: 'Alpha root',
      children: [
        createNode({
          uniqueIdentifier: IDS.prompt,
          name: '@ai Summarize https://example.com/spec',
          taskState: TASK_STATE.TODO,
          links: {
            [IDS.linked]: {
              id: IDS.linked,
              relationType: 1,
            },
          },
          children: [
            createNode({
              uniqueIdentifier: IDS.child,
              name: 'Important child',
            }),
          ],
        }),
      ],
    }));
    const beta = createSnapshot('Beta.json', createNode({
      uniqueIdentifier: IDS.linked,
      name: 'Linked target',
      children: [
        createNode({
          uniqueIdentifier: IDS.backlink,
          name: 'Backlink source',
          links: {
            [IDS.prompt]: {
              id: IDS.prompt,
              relationType: 3,
            },
          },
        }),
      ],
    }));

    const context = buildLlmContext({
      snapshot: alpha,
      nodeId: IDS.prompt,
      snapshots: [alpha, beta],
    });

    assert.equal(context.prompt.text, 'Summarize https://example.com/spec');
    assert.equal(context.subtree.children[0].name, 'Important child');
    assert.equal(context.links.outgoing[0].nodeId, IDS.linked);
    assert.equal(context.links.backlinks[0].nodeId, IDS.backlink);
    assert.deepEqual(context.urls.map((entry) => entry.url), ['https://example.com/spec']);

    const markdown = formatLlmContextMarkdown(context);
    assert.match(markdown, /## Tree/);
    assert.match(markdown, /Outgoing Links/);
    assert.match(markdown, /Backlinks/);
  });

  it('appends completed answers as text block children and marks the prompt done', () => {
    const snapshot = createSnapshot('Alpha.json', createNode({
      uniqueIdentifier: IDS.root,
      name: 'Alpha root',
      children: [
        createNode({
          uniqueIdentifier: IDS.prompt,
          name: '@ai Answer me',
          taskState: TASK_STATE.TODO,
        }),
      ],
    }));
    const job = createLlmJob({
      mapFilePath: snapshot.filePath,
      nodeId: IDS.prompt,
      prompt: 'Answer me',
    });

    const result = applyLlmJobCompletion(snapshot.document, job, {
      answer: 'Answer title\nAnswer body',
      agent: 'Codex',
      answerNodeId: IDS.answer,
      timestamp: '2026-05-18T09:00:00Z',
    });

    assert.equal(result.ok, true);
    const promptNode = snapshot.document.rootNode.children[0];
    assert.equal(promptNode.taskState, TASK_STATE.DONE);
    assert.equal(promptNode.children[0].nodeType, 2);
    assert.equal(promptNode.children[0].name, 'Answer title\nAnswer body');
    assert.equal(promptNode.children[0].metadata.source, 'llm:Codex');
  });
});
