export const MAX_VOICE_NOTE_MS = 5 * 60 * 1000;

export const VOICE_MIME_CANDIDATES = [
  'audio/webm;codecs=opus',
  'audio/webm',
  'audio/ogg;codecs=opus',
  'audio/mp4',
  'audio/mpeg',
];

export function selectVoiceMimeType(mediaRecorderCtor = globalThis.MediaRecorder) {
  if (!mediaRecorderCtor || typeof mediaRecorderCtor.isTypeSupported !== 'function') {
    return '';
  }

  return VOICE_MIME_CANDIDATES.find((candidate) => mediaRecorderCtor.isTypeSupported(candidate)) || '';
}

export function getVoiceFileExtension(mimeType) {
  const normalized = String(mimeType || '').toLowerCase();
  if (normalized.includes('ogg')) return '.ogg';
  if (normalized.includes('mp4')) return '.m4a';
  if (normalized.includes('mpeg') || normalized.includes('mp3')) return '.mp3';
  return '.webm';
}

export function buildVoiceNoteDisplayName(date = new Date()) {
  return `Voice note ${formatVoiceDate(date)}`;
}

export function buildVoiceNoteFileName(date = new Date(), mimeType = '') {
  return `${buildVoiceNoteDisplayName(date).replace(':', '-')}${getVoiceFileExtension(mimeType)}`;
}

export function createVoiceNoteFile(blob, options = {}) {
  const {
    date = new Date(),
    mimeType = blob?.type || '',
    FileCtor = globalThis.File,
  } = options;
  const displayName = buildVoiceNoteDisplayName(date);
  const fileName = `${displayName.replace(':', '-')}${getVoiceFileExtension(mimeType)}`;
  const fileOptions = { type: mimeType || blob?.type || 'audio/webm' };
  const file = typeof FileCtor === 'function'
    ? new FileCtor([blob], fileName, fileOptions)
    : new Blob([blob], fileOptions);

  if (!('name' in file)) {
    Object.defineProperty(file, 'name', {
      value: fileName,
      configurable: true,
    });
  }

  return {
    file,
    displayName,
  };
}

export function stopMediaStream(stream) {
  stream?.getTracks?.().forEach((track) => {
    try {
      track.stop();
    } catch {
    }
  });
}

export function formatVoiceElapsedTime(elapsedMs) {
  const totalSeconds = Math.max(0, Math.floor(Number(elapsedMs || 0) / 1000));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${String(seconds).padStart(2, '0')}`;
}

function formatVoiceDate(date) {
  const safeDate = date instanceof Date && !Number.isNaN(date.valueOf()) ? date : new Date();
  const year = safeDate.getFullYear();
  const month = String(safeDate.getMonth() + 1).padStart(2, '0');
  const day = String(safeDate.getDate()).padStart(2, '0');
  const hours = String(safeDate.getHours()).padStart(2, '0');
  const minutes = String(safeDate.getMinutes()).padStart(2, '0');
  return `${year}-${month}-${day} ${hours}:${minutes}`;
}
