import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { buildConflictResolveCommitMessage } from '../gitProvider/commitMessages.js';
import { GitHubApiError } from '../gitProvider/adapters/githubAdapter.js';
import {
  GitHubMindMapProvider,
  UNREADABLE_MAP_REASON,
  classifyUnreadableMapReason,
} from './githubMindMapProvider.js';

function createDocument(overrides = {}) {
  const rootNode = overrides.rootNode ?? {
    nodeType: 0,
    uniqueIdentifier: '53ba90f9-f653-4771-bc08-3c8a531b9b85',
    name: 'Map Root',
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

function createConflictError(message = 'Remote file changed during sync.') {
  return new GitHubApiError({
    code: 'CONFLICT',
    status: 409,
    statusText: 'Conflict',
    operation: 'putContent',
    contextLabel: 'saving remote file FocusMaps/Conflicted Map.json',
    message,
  });
}

describe('classifyUnreadableMapReason', () => {
  it('classifies unresolved Git merge markers before generic JSON parse failures', () => {
    const rawText = [
      '<<<<<<< HEAD',
      '{',
      '  "rootNode": { "name": "left" }',
      '=======',
      '{',
      '  "rootNode": { "name": "right" }',
      '>>>>>>> main',
    ].join('\n');

    const reason = classifyUnreadableMapReason(rawText, new SyntaxError('Unexpected token <'));

    assert.equal(reason, UNREADABLE_MAP_REASON.MERGE_CONFLICT);
  });

  it('classifies failed conflict auto-resolution explicitly', () => {
    const reason = classifyUnreadableMapReason(
      '<<<<<<< HEAD\nleft\n=======\nright\n>>>>>>> main\n',
      { code: 'AUTO_RESOLVE_FAILED' },
    );

    assert.equal(reason, UNREADABLE_MAP_REASON.AUTO_RESOLVE_FAILED);
  });

  it('classifies malformed JSON without conflict markers as invalid JSON', () => {
    const reason = classifyUnreadableMapReason('{"rootNode": ', new SyntaxError('Unexpected end of JSON input'));

    assert.equal(reason, UNREADABLE_MAP_REASON.INVALID_JSON);
  });
});

describe('GitHubMindMapProvider.loadMap', () => {
  it('auto-resolves conflicted files, saves the cleaned JSON, and returns the new revision', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });
    const ours = createDocument({
      updatedAt: '2026-04-20T08:00:00Z',
      rootNode: {
        ...createDocument().rootNode,
        name: 'Older name',
        metadata: {
          ...createDocument().rootNode.metadata,
          updatedAtUtc: '2026-04-20T08:00:00Z',
        },
      },
    });
    const theirs = createDocument({
      updatedAt: '2026-04-20T08:05:00Z',
      rootNode: {
        ...createDocument().rootNode,
        name: 'Newer name',
        metadata: {
          ...createDocument().rootNode.metadata,
          updatedAtUtc: '2026-04-20T08:05:00Z',
        },
      },
    });

    const calls = [];
    provider.gitProvider = {
      async getFile(path) {
        calls.push(['getFile', path]);
        return {
          content: createConflict(ours, theirs),
          versionToken: 'rev-conflicted',
        };
      },
      async putFile(path, content, versionToken, commitMessage) {
        calls.push(['putFile', path, versionToken, commitMessage, content]);
        return {
          versionToken: 'rev-resolved',
        };
      },
    };

    const loaded = await provider.loadMap('FocusMaps/Conflicted Map.json');

    assert.equal(loaded.revision, 'rev-resolved');
    assert.equal(loaded.document.rootNode.name, 'Newer name');
    assert.deepEqual(calls.slice(0, 2).map((call) => call.slice(0, 4)), [
      ['getFile', 'FocusMaps/Conflicted Map.json'],
      [
        'putFile',
        'FocusMaps/Conflicted Map.json',
        'rev-conflicted',
        buildConflictResolveCommitMessage('Conflicted Map'),
      ],
    ]);
    const savedDocument = JSON.parse(calls[1][4]);
    assert.equal(savedDocument.rootNode.name, 'Newer name');
  });

  it('retries auto-resolution once when the save hits a remote conflict', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });
    const baseRoot = createDocument().rootNode;
    const initialConflict = createConflict(
      createDocument({
        updatedAt: '2026-04-20T08:00:00Z',
        rootNode: {
          ...baseRoot,
          name: 'Older name',
          metadata: {
            ...baseRoot.metadata,
            updatedAtUtc: '2026-04-20T08:00:00Z',
          },
        },
      }),
      createDocument({
        updatedAt: '2026-04-20T08:05:00Z',
        rootNode: {
          ...baseRoot,
          name: 'Initial newer name',
          metadata: {
            ...baseRoot.metadata,
            updatedAtUtc: '2026-04-20T08:05:00Z',
          },
        },
      }),
    );
    const retriedConflict = createConflict(
      createDocument({
        updatedAt: '2026-04-20T08:06:00Z',
        rootNode: {
          ...baseRoot,
          name: 'Mid-merge name',
          metadata: {
            ...baseRoot.metadata,
            updatedAtUtc: '2026-04-20T08:06:00Z',
          },
        },
      }),
      createDocument({
        updatedAt: '2026-04-20T08:07:00Z',
        rootNode: {
          ...baseRoot,
          name: 'Latest name',
          metadata: {
            ...baseRoot.metadata,
            updatedAtUtc: '2026-04-20T08:07:00Z',
          },
        },
      }),
    );

    let getFileCount = 0;
    let putFileCount = 0;
    provider.gitProvider = {
      async getFile() {
        getFileCount += 1;
        return getFileCount === 1
          ? { content: initialConflict, versionToken: 'rev-1' }
          : { content: retriedConflict, versionToken: 'rev-2' };
      },
      async putFile(_path, content) {
        putFileCount += 1;
        if (putFileCount === 1) {
          throw createConflictError();
        }

        const savedDocument = JSON.parse(content);
        assert.equal(savedDocument.rootNode.name, 'Latest name');
        return {
          versionToken: 'rev-3',
        };
      },
    };

    const loaded = await provider.loadMap('FocusMaps/Conflicted Map.json');

    assert.equal(getFileCount, 2);
    assert.equal(putFileCount, 2);
    assert.equal(loaded.revision, 'rev-3');
    assert.equal(loaded.document.rootNode.name, 'Latest name');
  });

  it('returns the latest clean remote file after an auto-resolve save conflict', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });
    const conflicted = createConflict(
      createDocument({
        updatedAt: '2026-04-20T08:00:00Z',
      }),
      createDocument({
        updatedAt: '2026-04-20T08:05:00Z',
      }),
    );
    const cleanRemote = JSON.stringify(createDocument({
      updatedAt: '2026-04-20T08:06:00Z',
      rootNode: {
        ...createDocument().rootNode,
        name: 'Resolved elsewhere',
      },
    }), null, 2);

    let getFileCount = 0;
    let putFileCount = 0;
    provider.gitProvider = {
      async getFile() {
        getFileCount += 1;
        return getFileCount === 1
          ? { content: conflicted, versionToken: 'rev-1' }
          : { content: cleanRemote, versionToken: 'rev-2' };
      },
      async putFile() {
        putFileCount += 1;
        throw createConflictError();
      },
    };

    const loaded = await provider.loadMap('FocusMaps/Conflicted Map.json');

    assert.equal(getFileCount, 2);
    assert.equal(putFileCount, 1);
    assert.equal(loaded.revision, 'rev-2');
    assert.equal(loaded.document.rootNode.name, 'Resolved elsewhere');
  });

  it('surfaces auto-resolve failure when the resolver cannot safely merge the file', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });
    const conflicted = createConflict(
      {
        rootNode: {
          uniqueIdentifier: '53ba90f9-f653-4771-bc08-3c8a531b9b85',
        },
        customPreference: 'left',
      },
      {
        rootNode: {
          uniqueIdentifier: '53ba90f9-f653-4771-bc08-3c8a531b9b85',
        },
        customPreference: 'right',
      },
    );

    provider.gitProvider = {
      async getFile() {
        return {
          content: conflicted,
          versionToken: 'rev-conflicted',
        };
      },
      async putFile() {
        throw new Error('putFile should not be called when resolution fails');
      },
    };

    await assert.rejects(
      provider.loadMap('FocusMaps/Conflicted Map.json'),
      (error) => {
        assert.equal(error.code, 'UNREADABLE_MAP');
        assert.equal(error.reason, UNREADABLE_MAP_REASON.AUTO_RESOLVE_FAILED);
        assert.equal(error.revision, 'rev-conflicted');
        assert.match(error.message, /couldn't be auto-resolved/i);
        return true;
      },
    );
  });

  it('surfaces auto-resolve failure when the final retry save also conflicts', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });
    const conflicted = createConflict(
      createDocument({
        updatedAt: '2026-04-20T08:00:00Z',
      }),
      createDocument({
        updatedAt: '2026-04-20T08:05:00Z',
      }),
    );

    let getFileCount = 0;
    let putFileCount = 0;
    provider.gitProvider = {
      async getFile() {
        getFileCount += 1;
        return {
          content: conflicted,
          versionToken: getFileCount === 1 ? 'rev-1' : 'rev-2',
        };
      },
      async putFile() {
        putFileCount += 1;
        throw createConflictError();
      },
    };

    await assert.rejects(
      provider.loadMap('FocusMaps/Conflicted Map.json'),
      (error) => {
        assert.equal(getFileCount, 2);
        assert.equal(putFileCount, 2);
        assert.equal(error.code, 'UNREADABLE_MAP');
        assert.equal(error.reason, UNREADABLE_MAP_REASON.AUTO_RESOLVE_FAILED);
        return true;
      },
    );
  });
});

describe('GitHubMindMapProvider attachment paths', () => {
  const nodeId = '53ba90f9-f653-4771-bc08-3c8a531b9b85';
  const expectedPath = `FocusMaps/_attachments/${nodeId}/note.png`;

  it('loads attachment blobs from the node-scoped attachment directory', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });

    const calls = [];
    provider.gitProvider = {
      async getFileAsBlob(path, mediaType) {
        calls.push(['getFileAsBlob', path, mediaType]);
        return new Blob(['data'], { type: mediaType });
      },
    };

    const blob = await provider.getAttachmentBlob('FocusMaps/Alpha.json', nodeId, 'note.png', 'image/png');

    assert.equal(blob.type, 'image/png');
    assert.deepEqual(calls, [['getFileAsBlob', expectedPath, 'image/png']]);
  });

  it('uploads attachments into the node-scoped attachment directory', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });

    const calls = [];
    provider.gitProvider = {
      async putBinaryFile(path, base64Content, versionToken, commitMessage) {
        calls.push(['putBinaryFile', path, base64Content, versionToken, commitMessage]);
        return { versionToken: 'attachment-rev-1' };
      },
    };

    const result = await provider.uploadAttachment({
      mapFilePath: 'FocusMaps/Alpha.json',
      nodeId,
      relativePath: 'note.png',
      base64Content: 'ZGF0YQ==',
      commitMessage: 'Upload attachment',
    });

    assert.deepEqual(result, { ok: true, versionToken: 'attachment-rev-1' });
    assert.deepEqual(calls, [[
      'putBinaryFile',
      expectedPath,
      'ZGF0YQ==',
      null,
      'Upload attachment',
    ]]);
  });
});

describe('GitHubMindMapProvider.deleteAttachment', () => {
  const nodeId = '53ba90f9-f653-4771-bc08-3c8a531b9b85';

  it('loads the current remote attachment sha before deleting when none was provided', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });

    const calls = [];
    provider.gitProvider = {
      async getFileRaw(path) {
        calls.push(['getFileRaw', path]);
        return {
          base64Content: 'ZGF0YQ==',
          versionToken: 'attachment-sha-123',
        };
      },
      async deleteFile(path, versionToken, commitMessage) {
        calls.push(['deleteFile', path, versionToken, commitMessage]);
        return {
          commitSha: 'commit-sha',
        };
      },
    };

    const result = await provider.deleteAttachment({
      mapFilePath: 'FocusMaps/Alpha.json',
      nodeId,
      relativePath: 'note.png',
      versionToken: null,
      commitMessage: 'Delete attachment',
    });

    assert.deepEqual(result, { ok: true });
    assert.deepEqual(calls, [
      ['getFileRaw', `FocusMaps/_attachments/${nodeId}/note.png`],
      ['deleteFile', `FocusMaps/_attachments/${nodeId}/note.png`, 'attachment-sha-123', 'Delete attachment'],
    ]);
  });

  it('treats a missing remote attachment as already deleted when loading its sha fails', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });

    provider.gitProvider = {
      async getFileRaw() {
        throw new GitHubApiError({
          code: 'NOT_FOUND',
          status: 404,
          statusText: 'Not Found',
          operation: 'getContent',
          contextLabel: `loading raw file FocusMaps/_attachments/${nodeId}/note.txt`,
          message: 'Remote file was not found (404 Not Found).',
        });
      },
      async deleteFile() {
        throw new Error('deleteFile should not be called when getFileRaw fails with NOT_FOUND');
      },
    };

    const result = await provider.deleteAttachment({
      mapFilePath: 'FocusMaps/Alpha.json',
      nodeId,
      relativePath: 'note.txt',
      versionToken: null,
      commitMessage: 'Delete attachment',
    });

    assert.deepEqual(result, { ok: true });
  });

  it('treats a missing remote attachment as already deleted when delete returns NOT_FOUND', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });

    provider.gitProvider = {
      async getFileRaw() {
        return {
          base64Content: 'ZGF0YQ==',
          versionToken: 'attachment-sha-456',
        };
      },
      async deleteFile() {
        throw new GitHubApiError({
          code: 'NOT_FOUND',
          status: 404,
          statusText: 'Not Found',
          operation: 'deleteContent',
          contextLabel: `deleting remote file FocusMaps/_attachments/${nodeId}/note.txt`,
          message: 'GitHub resource was not found (404 Not Found) while deleting remote file.',
        });
      },
    };

    const result = await provider.deleteAttachment({
      mapFilePath: 'FocusMaps/Alpha.json',
      nodeId,
      relativePath: 'note.txt',
      versionToken: null,
      commitMessage: 'Delete attachment',
    });

    assert.deepEqual(result, { ok: true });
  });
});
