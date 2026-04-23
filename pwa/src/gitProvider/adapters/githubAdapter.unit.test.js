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

describe('GitHubAdapter raw content requests', () => {
  it('requests large raw blobs with GitHub raw+json media type', async () => {
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

    const blob = await adapter.getContentBlob(
      'FocusMaps/_attachments/node-id/camera-photo.jpg',
      'loading raw camera photo',
    );

    assert.equal(await blob.text(), 'camera-photo');
    assert.equal(calls.length, 1);
    assert.equal(
      calls[0][0],
      'https://api.github.com/repos/octocat/focus/contents/FocusMaps/_attachments/node-id/camera-photo.jpg?ref=main',
    );
    assert.equal(calls[0][1].headers.Accept, 'application/vnd.github.raw+json');
    assert.equal(calls[0][1].headers.Authorization, 'Bearer test-token');
    assert.equal(calls[0][1].headers['X-GitHub-Api-Version'], '2022-11-28');
  });

  it('requests large raw text with GitHub raw+json media type', async () => {
    const calls = [];
    const adapter = createAdapter(async (input, init) => {
      calls.push([String(input), init]);
      return {
        ok: true,
        async text() {
          return 'attachment-text';
        },
      };
    });

    const text = await adapter.getContentText(
      'FocusMaps/_attachments/node-id/note.txt',
      'loading raw note',
    );

    assert.equal(text, 'attachment-text');
    assert.equal(calls.length, 1);
    assert.equal(
      calls[0][0],
      'https://api.github.com/repos/octocat/focus/contents/FocusMaps/_attachments/node-id/note.txt?ref=main',
    );
    assert.equal(calls[0][1].headers.Accept, 'application/vnd.github.raw+json');
  });
});
