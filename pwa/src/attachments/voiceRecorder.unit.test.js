import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildVoiceNoteDisplayName,
  buildVoiceNoteFileName,
  createVoiceNoteFile,
  formatVoiceElapsedTime,
  getVoiceFileExtension,
  selectVoiceMimeType,
  stopMediaStream,
} from './voiceRecorder.js';

describe('voice recorder helpers', () => {
  it('selects the first supported compressed audio type', () => {
    const checkedTypes = [];
    const mediaRecorderCtor = {
      isTypeSupported(type) {
        checkedTypes.push(type);
        return type === 'audio/ogg;codecs=opus';
      },
    };

    assert.equal(selectVoiceMimeType(mediaRecorderCtor), 'audio/ogg;codecs=opus');
    assert.deepEqual(checkedTypes, [
      'audio/webm;codecs=opus',
      'audio/webm',
      'audio/ogg;codecs=opus',
    ]);
  });

  it('falls back to an empty mime type when MediaRecorder support cannot be queried', () => {
    assert.equal(selectVoiceMimeType(null), '');
    assert.equal(selectVoiceMimeType({}), '');
  });

  it('maps audio mime types to useful file extensions', () => {
    assert.equal(getVoiceFileExtension('audio/webm;codecs=opus'), '.webm');
    assert.equal(getVoiceFileExtension('audio/ogg;codecs=opus'), '.ogg');
    assert.equal(getVoiceFileExtension('audio/mp4'), '.m4a');
    assert.equal(getVoiceFileExtension('audio/mpeg'), '.mp3');
  });

  it('creates a voice note file name and display name', () => {
    const date = new Date(2026, 3, 27, 14, 5, 0);
    const blob = new Blob(['voice'], { type: 'audio/webm;codecs=opus' });
    const created = createVoiceNoteFile(blob, { date, mimeType: 'audio/webm;codecs=opus' });

    assert.equal(buildVoiceNoteDisplayName(date), 'Voice note 2026-04-27 14:05');
    assert.equal(buildVoiceNoteFileName(date, 'audio/webm;codecs=opus'), 'Voice note 2026-04-27 14-05.webm');
    assert.equal(created.displayName, 'Voice note 2026-04-27 14:05');
    assert.equal(created.file.name, 'Voice note 2026-04-27 14-05.webm');
    assert.equal(created.file.type, 'audio/webm;codecs=opus');
  });

  it('formats elapsed recording time and stops all stream tracks', () => {
    const stopped = [];
    stopMediaStream({
      getTracks() {
        return [
          { stop: () => stopped.push('a') },
          { stop: () => stopped.push('b') },
        ];
      },
    });

    assert.equal(formatVoiceElapsedTime(0), '0:00');
    assert.equal(formatVoiceElapsedTime(65_100), '1:05');
    assert.deepEqual(stopped, ['a', 'b']);
  });
});
