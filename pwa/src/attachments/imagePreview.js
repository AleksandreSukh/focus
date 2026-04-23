export const INLINE_IMAGE_PREVIEW_ERROR_MESSAGE =
  'This image was loaded, but the browser could not preview it inline. Download it to open externally.';

export async function loadImagePreview(blob, options = {}) {
  const createObjectUrl = options.createObjectUrl || ((value) => URL.createObjectURL(value));
  const imageUrl = createObjectUrl(blob);

  try {
    await decodeImagePreview(imageUrl, options);
    return {
      ok: true,
      imageUrl,
      errorMessage: '',
    };
  } catch (cause) {
    return {
      ok: false,
      imageUrl,
      errorMessage: INLINE_IMAGE_PREVIEW_ERROR_MESSAGE,
      cause,
    };
  }
}

async function decodeImagePreview(imageUrl, options) {
  const createImage = options.createImage || (() => new Image());
  const image = createImage();
  if (!image || typeof image !== 'object') {
    throw new Error('Unable to create preview image.');
  }

  await new Promise((resolve, reject) => {
    let settled = false;
    const settle = (callback, value) => {
      if (settled) {
        return;
      }

      settled = true;
      image.onload = null;
      image.onerror = null;
      callback(value);
    };
    const fail = (cause) => {
      const error = cause instanceof Error
        ? cause
        : new Error('Image preview decode failed.');
      settle(reject, error);
    };

    image.onload = () => settle(resolve);
    image.onerror = () => fail(new Error('Image preview decode failed.'));
    image.src = imageUrl;

    if (typeof image.decode === 'function') {
      image.decode().then(
        () => settle(resolve),
        fail,
      );
    }
  });
}
