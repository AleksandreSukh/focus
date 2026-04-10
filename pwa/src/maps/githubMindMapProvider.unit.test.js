import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { GitHubApiError } from '../gitProvider/adapters/githubAdapter.js';
import {
  GitHubMindMapProvider,
  UNREADABLE_MAP_REASON,
  classifyUnreadableMapReason,
} from './githubMindMapProvider.js';

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

  it('classifies malformed JSON without conflict markers as invalid JSON', () => {
    const reason = classifyUnreadableMapReason('{"rootNode": ', new SyntaxError('Unexpected end of JSON input'));

    assert.equal(reason, UNREADABLE_MAP_REASON.INVALID_JSON);
  });
});

describe('GitHubMindMapProvider.loadMap', () => {
  it('throws a structured unreadable-map error for conflicted files', async () => {
    const provider = new GitHubMindMapProvider({
      owner: 'octocat',
      repo: 'focus',
      branch: 'main',
      token: 'test-token',
      directoryPath: 'FocusMaps',
    });

    provider.gitProvider = {
      async getFile() {
        return {
          content: [
            '{',
            '<<<<<<< HEAD',
            '"rootNode": { "name": "left" }',
            '=======',
            '"rootNode": { "name": "right" }',
            '>>>>>>> main',
            '}',
          ].join('\n'),
          versionToken: 'rev-conflicted',
        };
      },
    };

    await assert.rejects(
      provider.loadMap('FocusMaps/Conflicted Map.json'),
      (error) => {
        assert.equal(error.code, 'UNREADABLE_MAP');
        assert.equal(error.reason, UNREADABLE_MAP_REASON.MERGE_CONFLICT);
        assert.equal(error.filePath, 'FocusMaps/Conflicted Map.json');
        assert.equal(error.fileName, 'Conflicted Map.json');
        assert.equal(error.mapName, 'Conflicted Map');
        assert.equal(error.revision, 'rev-conflicted');
        assert.match(error.message, /merge markers/i);
        assert.match(error.rawText, /<<<<<<< HEAD/);
        return true;
      },
    );
  });
});

describe('GitHubMindMapProvider.deleteAttachment', () => {
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
      relativePath: 'note.png',
      versionToken: null,
      commitMessage: 'Delete attachment',
    });

    assert.deepEqual(result, { ok: true });
    assert.deepEqual(calls, [
      ['getFileRaw', 'FocusMaps/Alpha_attachments/note.png'],
      ['deleteFile', 'FocusMaps/Alpha_attachments/note.png', 'attachment-sha-123', 'Delete attachment'],
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
          contextLabel: 'loading raw file FocusMaps/Alpha_attachments/note.txt',
          message: 'Remote file was not found (404 Not Found).',
        });
      },
      async deleteFile() {
        throw new Error('deleteFile should not be called when getFileRaw fails with NOT_FOUND');
      },
    };

    const result = await provider.deleteAttachment({
      mapFilePath: 'FocusMaps/Alpha.json',
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
          contextLabel: 'deleting remote file FocusMaps/Alpha_attachments/note.txt',
          message: 'GitHub resource was not found (404 Not Found) while deleting remote file.',
        });
      },
    };

    const result = await provider.deleteAttachment({
      mapFilePath: 'FocusMaps/Alpha.json',
      relativePath: 'note.txt',
      versionToken: null,
      commitMessage: 'Delete attachment',
    });

    assert.deepEqual(result, { ok: true });
  });
});
