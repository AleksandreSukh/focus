import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';

const TOOL_PATH = path.resolve('tools/focus-interop');

const IDS = {
  root: '11111111-1111-4111-8111-111111111111',
  prompt: '22222222-2222-4222-8222-222222222222',
  linked: '33333333-3333-4333-8333-333333333333',
  backlink: '44444444-4444-4444-8444-444444444444',
};

function createTempMapsDir() {
  return fs.mkdtempSync(path.join(os.tmpdir(), 'focus-interop-'));
}

function writeJson(filePath, value) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, `${JSON.stringify(value, null, 2)}\n`, 'utf8');
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
    taskState: overrides.taskState ?? 0,
    metadata: {
      createdAtUtc: '2026-05-18T08:00:00Z',
      updatedAtUtc: '2026-05-18T08:00:00Z',
      source: 'manual',
      device: 'test',
      attachments: [],
    },
  };
}

function writeFixtureWorkspace(mapsDir) {
  writeJson(path.join(mapsDir, 'Alpha.json'), {
    updatedAt: '2026-05-18T08:00:00Z',
    rootNode: createNode({
      uniqueIdentifier: IDS.root,
      name: 'Alpha',
      children: [
        createNode({
          uniqueIdentifier: IDS.prompt,
          name: '@ai Summarize https://example.com',
          taskState: 1,
          links: {
            [IDS.linked]: {
              id: IDS.linked,
              relationType: 1,
            },
          },
        }),
      ],
    }),
  });
  writeJson(path.join(mapsDir, 'Beta.json'), {
    updatedAt: '2026-05-18T08:00:00Z',
    rootNode: createNode({
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
    }),
  });
  writeJson(path.join(mapsDir, '_llm', 'jobs', 'job-1.json'), {
    version: 1,
    id: 'job-1',
    status: 'pending',
    mode: 'subtree-links',
    mapFilePath: 'FocusMaps/Alpha.json',
    nodeId: IDS.prompt,
    prompt: 'Summarize https://example.com',
    createdAt: '2026-05-18T08:00:00Z',
    updatedAt: '2026-05-18T08:00:00Z',
  });
}

function runTool(args) {
  return execFileSync(process.execPath, [TOOL_PATH, ...args], {
    cwd: path.resolve('.'),
    encoding: 'utf8',
  });
}

describe('focus-interop CLI', () => {
  it('prints JSON context with tree, links, and backlinks', () => {
    const mapsDir = createTempMapsDir();
    writeFixtureWorkspace(mapsDir);

    const output = runTool([
      'context',
      '--maps-dir', mapsDir,
      '--map', 'Alpha.json',
      '--node', IDS.prompt,
      '--format', 'json',
    ]);
    const context = JSON.parse(output);

    assert.equal(context.prompt.text, 'Summarize https://example.com');
    assert.equal(context.subtree.nodeId, IDS.prompt);
    assert.equal(context.links.outgoing[0].nodeId, IDS.linked);
    assert.equal(context.links.backlinks[0].nodeId, IDS.backlink);
  });

  it('claims a job by updating only the sidecar file', () => {
    const mapsDir = createTempMapsDir();
    writeFixtureWorkspace(mapsDir);
    const beforeMap = fs.readFileSync(path.join(mapsDir, 'Alpha.json'), 'utf8');

    runTool([
      'jobs',
      'claim',
      '--maps-dir', mapsDir,
      '--agent', 'Codex',
      '--format', 'json',
    ]);

    const job = JSON.parse(fs.readFileSync(path.join(mapsDir, '_llm', 'jobs', 'job-1.json'), 'utf8'));
    const afterMap = fs.readFileSync(path.join(mapsDir, 'Alpha.json'), 'utf8');

    assert.equal(job.status, 'claimed');
    assert.equal(job.claimedBy, 'Codex');
    assert.equal(afterMap, beforeMap);
  });

  it('completes a job by appending a text block answer and marking the prompt done', () => {
    const mapsDir = createTempMapsDir();
    writeFixtureWorkspace(mapsDir);
    const answerPath = path.join(mapsDir, 'answer.md');
    fs.writeFileSync(answerPath, 'Answer title\nAnswer body', 'utf8');

    runTool([
      'jobs',
      'claim',
      '--maps-dir', mapsDir,
      '--agent', 'Codex',
    ]);
    runTool([
      'jobs',
      'complete',
      '--maps-dir', mapsDir,
      '--job', 'job-1',
      '--answer-file', answerPath,
    ]);

    const map = JSON.parse(fs.readFileSync(path.join(mapsDir, 'Alpha.json'), 'utf8'));
    const prompt = map.rootNode.children[0];
    const answer = prompt.children[0];
    const job = JSON.parse(fs.readFileSync(path.join(mapsDir, '_llm', 'jobs', 'job-1.json'), 'utf8'));

    assert.equal(prompt.taskState, 3);
    assert.equal(answer.nodeType, 2);
    assert.equal(answer.name, 'Answer title\nAnswer body');
    assert.equal(answer.metadata.source, 'llm:Codex');
    assert.equal(job.status, 'completed');
    assert.equal(job.result.promptNodeId, IDS.prompt);
    assert.equal(job.result.answerNodeId, answer.uniqueIdentifier);
  });
});
