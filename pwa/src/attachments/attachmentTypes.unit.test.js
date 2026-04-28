import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  getAttachmentViewerKind,
  isAudioAttachment,
  isTextAttachment,
} from './attachmentTypes.js';

describe('attachment type helpers', () => {
  it('routes audio attachments to the audio viewer', () => {
    const attachment = {
      mediaType: 'audio/webm; codecs=opus',
    };

    assert.equal(isAudioAttachment(attachment), true);
    assert.equal(isTextAttachment(attachment), false);
    assert.equal(getAttachmentViewerKind(attachment), 'audioViewer');
  });

  it('keeps text and non-audio binary attachments on their existing viewers', () => {
    assert.equal(getAttachmentViewerKind({ mediaType: 'text/plain' }), 'textViewer');
    assert.equal(getAttachmentViewerKind({ mediaType: 'image/png' }), 'imageViewer');
    assert.equal(getAttachmentViewerKind({ mediaType: 'application/pdf' }), 'imageViewer');
  });
});
