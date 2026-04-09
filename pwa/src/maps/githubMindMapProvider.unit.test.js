import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
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
