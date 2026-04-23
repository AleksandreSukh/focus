import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { GitHubAdapter } from './githubAdapter.js';

function createAdapter(fetchImpl) {
  return new GitHubAdapter({
    owner: 'octocat',
    repo: 'focus',
    branch: 'main',
    token: 'test-token',
    fetchImpl,
  });
}

describe('GitHubAdapter content requests', () => {
  it('requests file metadata with no-store cache semantics', async () => {
    const calls = [];
    const adapter = createAdapter(async (input, init) => {
      calls.push([String(input), init]);
      return {
        ok: true,
        async json() {
          return {
            sha: 'blob-sha-1',
            content: '',
            encoding: 'none',
          };
        },
      };
    });

    const response = await adapter.getContent(
      'FocusMaps/_attachments/node-id/camera-photo.jpg',
      'loading attachment metadata',
    );

    assert.deepEqual(response, {
      sha: 'blob-sha-1',
      content: '',
      encoding: 'none',
    });
    assert.equal(calls.length, 1);
    assert.equal(
      calls[0][0],
      'https://api.github.com/repos/octocat/focus/contents/FocusMaps/_attachments/node-id/camera-photo.jpg?ref=main',
    );
    assert.equal(calls[0][1].cache, 'no-store');
    assert.equal(calls[0][1].headers.Accept, 'application/vnd.github+json');
    assert.equal(calls[0][1].headers.Authorization, 'Bearer test-token');
    assert.equal(calls[0][1].headers['X-GitHub-Api-Version'], '2022-11-28');
  });

  it('requests large raw blobs by blob sha with no-store cache semantics', async () => {
    const calls = [];
    const adapter = createAdapter(async (input, init) => {
      calls.push([String(input), init]);
      return {
        ok: true,
        async blob() {
          return new Blob(['camera-photo'], { type: 'image/jpeg' });
        },
      };
    });

    const blob = await adapter.getBlob(
      'camera-photo-sha',
      'loading raw camera photo',
    );

    assert.equal(await blob.text(), 'camera-photo');
    assert.equal(calls.length, 1);
    assert.equal(
      calls[0][0],
      'https://api.github.com/repos/octocat/focus/git/blobs/camera-photo-sha',
    );
    assert.equal(calls[0][1].cache, 'no-store');
    assert.equal(calls[0][1].headers.Accept, 'application/vnd.github.raw+json');
  });
});
