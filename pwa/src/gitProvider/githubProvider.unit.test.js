import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { GitHubProvider } from './githubProvider.js';

function createProvider(adapterOverrides = {}) {
  const provider = new GitHubProvider({
    owner: 'octocat',
    repo: 'focus',
    branch: 'main',
    token: 'test-token',
  });

  provider.adapter = {
    async getContent() {
      throw new Error('getContent was not mocked for this test.');
    },
    async getBlob() {
      throw new Error('getBlob was not mocked for this test.');
    },
    async getContentBlob() {
      throw new Error('getContentBlob was not mocked for this test.');
    },
    async getContentText() {
      throw new Error('getContentText was not mocked for this test.');
    },
    ...adapterOverrides,
  };

  return provider;
}

describe('GitHubProvider.getFile', () => {
  it('falls back to blob-by-sha loading when GitHub reports encoding none', async () => {
    const calls = [];
    const provider = createProvider({
      async getContent(path, contextLabel) {
        calls.push(['getContent', path, contextLabel]);
        return {
          encoding: 'none',
          content: '',
          sha: 'rev-1',
        };
      },
      async getBlob(blobSha, contextLabel) {
        calls.push(['getBlob', blobSha, contextLabel]);
        return new Blob(['{"ok":true}'], { type: 'application/json' });
      },
    });

    const result = await provider.getFile('FocusMaps/map.json');

    assert.deepEqual(result, {
      content: '{"ok":true}',
      versionToken: 'rev-1',
    });
    assert.deepEqual(calls, [
      ['getContent', 'FocusMaps/map.json', 'loading remote file FocusMaps/map.json'],
      ['getBlob', 'rev-1', 'loading remote file FocusMaps/map.json'],
    ]);
  });
});

describe('GitHubProvider.getFileRaw', () => {
  it('falls back to blob-by-sha loading and re-encodes content as base64 when GitHub reports encoding none', async () => {
    const calls = [];
    const provider = createProvider({
      async getContent(path, contextLabel) {
        calls.push(['getContent', path, contextLabel]);
        return {
          encoding: 'none',
          content: '',
          sha: 'attachment-rev-1',
        };
      },
      async getBlob(blobSha, contextLabel) {
        calls.push(['getBlob', blobSha, contextLabel]);
        return new Blob(['data'], { type: 'application/octet-stream' });
      },
    });

    const result = await provider.getFileRaw('FocusMaps/_attachments/node-id/note.png');

    assert.deepEqual(result, {
      base64Content: 'ZGF0YQ==',
      versionToken: 'attachment-rev-1',
    });
    assert.deepEqual(calls, [
      [
        'getContent',
        'FocusMaps/_attachments/node-id/note.png',
        'loading raw file FocusMaps/_attachments/node-id/note.png',
      ],
      [
        'getBlob',
        'attachment-rev-1',
        'loading raw file FocusMaps/_attachments/node-id/note.png',
      ],
    ]);
  });
});

describe('GitHubProvider.getFileAsBlob', () => {
  it('falls back to blob-by-sha loading and preserves the requested media type when GitHub reports encoding none', async () => {
    const calls = [];
    const provider = createProvider({
      async getContent(path, contextLabel) {
        calls.push(['getContent', path, contextLabel]);
        return {
          encoding: 'none',
          content: '',
          sha: 'attachment-rev-2',
        };
      },
      async getBlob(blobSha, contextLabel) {
        calls.push(['getBlob', blobSha, contextLabel]);
        return new Blob(['image-bytes'], { type: '' });
      },
    });

    const blob = await provider.getFileAsBlob(
      'FocusMaps/_attachments/node-id/image.png',
      'image/png',
    );

    assert.equal(blob.type, 'image/png');
    assert.equal(await blob.text(), 'image-bytes');
    assert.deepEqual(calls, [
      [
        'getContent',
        'FocusMaps/_attachments/node-id/image.png',
        'loading attachment FocusMaps/_attachments/node-id/image.png',
      ],
      [
        'getBlob',
        'attachment-rev-2',
        'loading attachment FocusMaps/_attachments/node-id/image.png',
      ],
    ]);
  });

  it('preserves the requested JPEG media type for large raw image attachments fetched by blob sha', async () => {
    const calls = [];
    const provider = createProvider({
      async getContent(path, contextLabel) {
        calls.push(['getContent', path, contextLabel]);
        return {
          encoding: 'none',
          content: '',
          sha: 'attachment-rev-3',
        };
      },
      async getBlob(blobSha, contextLabel) {
        calls.push(['getBlob', blobSha, contextLabel]);
        return new Blob(['camera-photo-bytes'], { type: '' });
      },
    });

    const blob = await provider.getFileAsBlob(
      'FocusMaps/_attachments/node-id/camera-photo.jpg',
      'image/jpeg',
    );

    assert.equal(blob.type, 'image/jpeg');
    assert.equal(await blob.text(), 'camera-photo-bytes');
    assert.deepEqual(calls, [
      [
        'getContent',
        'FocusMaps/_attachments/node-id/camera-photo.jpg',
        'loading attachment FocusMaps/_attachments/node-id/camera-photo.jpg',
      ],
      [
        'getBlob',
        'attachment-rev-3',
        'loading attachment FocusMaps/_attachments/node-id/camera-photo.jpg',
      ],
    ]);
  });
});
