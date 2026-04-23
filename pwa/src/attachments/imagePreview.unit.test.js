import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  INLINE_IMAGE_PREVIEW_ERROR_MESSAGE,
  loadImagePreview,
} from './imagePreview.js';

describe('loadImagePreview', () => {
  it('returns a usable preview URL when image decoding succeeds', async () => {
    const image = {
      src: '',
      onload: null,
      onerror: null,
      decodeCalls: 0,
      decode() {
        this.decodeCalls += 1;
        return Promise.resolve();
      },
    };
    const blob = new Blob(['preview-bytes'], { type: 'image/jpeg' });

    const result = await loadImagePreview(blob, {
      createObjectUrl(value) {
        assert.equal(value, blob);
        return 'blob:preview-success';
      },
      createImage() {
        return image;
      },
    });

    assert.deepEqual(result, {
      ok: true,
      imageUrl: 'blob:preview-success',
      errorMessage: '',
    });
    assert.equal(image.src, 'blob:preview-success');
    assert.equal(image.decodeCalls, 1);
  });

  it('keeps the preview URL available for download when inline decode fails', async () => {
    const image = {
      src: '',
      onload: null,
      onerror: null,
      decode() {
        return Promise.reject(new Error('decode failed'));
      },
    };

    const result = await loadImagePreview(new Blob(['camera-photo'], { type: 'image/jpeg' }), {
      createObjectUrl() {
        return 'blob:preview-failed';
      },
      createImage() {
        return image;
      },
    });

    assert.equal(result.ok, false);
    assert.equal(result.imageUrl, 'blob:preview-failed');
    assert.equal(result.errorMessage, INLINE_IMAGE_PREVIEW_ERROR_MESSAGE);
    assert.match(result.cause.message, /decode failed/i);
  });

  it('falls back to image load events when Image.decode is unavailable', async () => {
    const image = {
      _src: '',
      onload: null,
      onerror: null,
      set src(value) {
        this._src = value;
        queueMicrotask(() => {
          this.onload?.();
        });
      },
      get src() {
        return this._src;
      },
    };

    const result = await loadImagePreview(new Blob(['fallback-image'], { type: 'image/jpeg' }), {
      createObjectUrl() {
        return 'blob:preview-onload';
      },
      createImage() {
        return image;
      },
    });

    assert.deepEqual(result, {
      ok: true,
      imageUrl: 'blob:preview-onload',
      errorMessage: '',
    });
    assert.equal(image.src, 'blob:preview-onload');
  });
});
