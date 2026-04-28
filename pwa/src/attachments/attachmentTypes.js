export function isTextAttachment(attachment) {
  return typeof attachment?.mediaType === 'string' && attachment.mediaType.toLowerCase().startsWith('text/');
}

export function isAudioAttachment(attachment) {
  return typeof attachment?.mediaType === 'string' && attachment.mediaType.toLowerCase().startsWith('audio/');
}

export function getAttachmentViewerKind(attachment) {
  if (isTextAttachment(attachment)) {
    return 'textViewer';
  }

  if (isAudioAttachment(attachment)) {
    return 'audioViewer';
  }

  return 'imageViewer';
}
