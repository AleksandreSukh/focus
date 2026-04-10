import { GitHubApiError } from '../gitProvider/adapters/githubAdapter.js';
import { GitHubProvider } from '../gitProvider/githubProvider.js';
import {
  normalizeMindMapDocument,
  parseMindMapDocument,
  serializeMindMapDocument,
} from './model.js';

export const UNREADABLE_MAP_REASON = Object.freeze({
  MERGE_CONFLICT: 'mergeConflict',
  INVALID_JSON: 'invalidJson',
  UNKNOWN: 'unknown',
});

export class GitHubMindMapProvider {
  constructor({ owner, repo, branch, token, directoryPath }) {
    this.directoryPath = String(directoryPath ?? '').replace(/^\/+|\/+$/g, '');
    this.gitProvider = new GitHubProvider({
      owner,
      repo,
      branch,
      token,
    });
  }

  async listMapFiles() {
    const entries = await this.gitProvider.listDirectory(this.directoryPath);
    return entries
      .filter((entry) =>
        entry &&
        entry.type === 'file' &&
        typeof entry.name === 'string' &&
        entry.name.toLowerCase().endsWith('.json'),
      )
      .map((entry) => ({
        fileName: entry.name,
        filePath: entry.path,
      }))
      .sort((left, right) => left.fileName.localeCompare(right.fileName));
  }

  async loadMap(filePath) {
    const snapshot = await this.gitProvider.getFile(filePath);
    const fileName = filePath.split('/').pop() || filePath;
    const mapName = fileName.replace(/\.json$/i, '');

    try {
      return {
        filePath,
        fileName,
        mapName,
        document: normalizeMindMapDocument(parseMindMapDocument(snapshot.content)),
        revision: snapshot.versionToken,
        loadedAt: Date.now(),
      };
    } catch (cause) {
      throw createUnreadableMapError({
        filePath,
        fileName,
        mapName,
        revision: snapshot.versionToken,
        rawText: snapshot.content,
        cause,
      });
    }
  }

  async getAttachmentBlob(mapFilePath, relativePath, mediaType) {
    const attachmentDir = mapFilePath.replace(/\.json$/i, '_attachments');
    const attachmentPath = `${attachmentDir}/${relativePath}`;
    return this.gitProvider.getFileAsBlob(attachmentPath, mediaType || 'image/png');
  }

  async deleteMap({ filePath, expectedRevision, commitMessage }) {
    try {
      const result = await this.gitProvider.deleteFile(
        filePath,
        expectedRevision,
        commitMessage,
      );

      return {
        ok: true,
        commitSha: result.commitSha,
      };
    } catch (error) {
      if (error instanceof GitHubApiError && error.code === 'CONFLICT') {
        return {
          ok: false,
          reason: 'conflict',
        };
      }

      throw error;
    }
  }

  async uploadAttachment({ mapFilePath, relativePath, base64Content, commitMessage }) {
    const attachmentDir = mapFilePath.replace(/\.json$/i, '_attachments');
    const attachmentPath = `${attachmentDir}/${relativePath}`;
    const result = await this.gitProvider.putBinaryFile(
      attachmentPath,
      base64Content,
      null,
      commitMessage,
    );
    return { ok: true, versionToken: result.versionToken };
  }

  async deleteAttachment({ mapFilePath, relativePath, versionToken, commitMessage }) {
    const attachmentDir = mapFilePath.replace(/\.json$/i, '_attachments');
    const attachmentPath = `${attachmentDir}/${relativePath}`;
    let effectiveVersionToken = versionToken;

    try {
      if (!effectiveVersionToken) {
        const remoteAttachment = await this.gitProvider.getFileRaw(attachmentPath);
        effectiveVersionToken = remoteAttachment.versionToken;
      }

      await this.gitProvider.deleteFile(attachmentPath, effectiveVersionToken, commitMessage);
    } catch (error) {
      if (error instanceof GitHubApiError && error.code === 'NOT_FOUND') {
        return { ok: true };
      }

      throw error;
    }
    return { ok: true };
  }

  async renameAttachmentDirectory(oldMapFilePath, newMapFilePath, commitMessage) {
    const oldDir = oldMapFilePath.replace(/\.json$/i, '_attachments');
    const newDir = newMapFilePath.replace(/\.json$/i, '_attachments');

    let entries;
    try {
      entries = await this.gitProvider.listDirectory(oldDir);
    } catch (error) {
      if (error?.code === 'NOT_FOUND') return { ok: true };
      throw error;
    }

    const files = entries.filter((e) => e.type === 'file');
    if (files.length === 0) return { ok: true };

    for (const file of files) {
      const raw = await this.gitProvider.getFileRaw(file.path);
      await this.gitProvider.putBinaryFile(`${newDir}/${file.name}`, raw.base64Content, null, commitMessage);
      await this.gitProvider.deleteFile(file.path, raw.versionToken, commitMessage);
    }

    return { ok: true };
  }

  async saveMap({ filePath, document, expectedRevision, commitMessage }) {
    try {
      const result = await this.gitProvider.putFile(
        filePath,
        serializeMindMapDocument(normalizeMindMapDocument(document)),
        expectedRevision,
        commitMessage,
      );

      return {
        ok: true,
        revision: result.versionToken,
      };
    } catch (error) {
      if (error instanceof GitHubApiError && error.code === 'CONFLICT') {
        return {
          ok: false,
          reason: 'conflict',
        };
      }

      throw error;
    }
  }
}

export function classifyUnreadableMapReason(rawText, cause) {
  if (containsMergeConflictMarkers(rawText)) {
    return UNREADABLE_MAP_REASON.MERGE_CONFLICT;
  }

  if (cause instanceof SyntaxError) {
    return UNREADABLE_MAP_REASON.INVALID_JSON;
  }

  return UNREADABLE_MAP_REASON.UNKNOWN;
}

function createUnreadableMapError({
  filePath,
  fileName,
  mapName,
  revision,
  rawText,
  cause,
}) {
  const reason = classifyUnreadableMapReason(rawText, cause);
  const error = new Error(buildUnreadableMapMessage(fileName, reason));
  error.name = 'UnreadableMapError';
  error.code = 'UNREADABLE_MAP';
  error.reason = reason;
  error.filePath = filePath;
  error.fileName = fileName;
  error.mapName = mapName;
  error.revision = revision;
  error.rawText = typeof rawText === 'string' ? rawText : '';
  error.retriable = true;
  error.cause = cause;
  return error;
}

function buildUnreadableMapMessage(fileName, reason) {
  switch (reason) {
    case UNREADABLE_MAP_REASON.MERGE_CONFLICT:
      return `Map "${fileName}" contains unresolved Git merge markers and cannot be loaded.`;
    case UNREADABLE_MAP_REASON.INVALID_JSON:
      return `Map "${fileName}" is not valid JSON and cannot be loaded.`;
    default:
      return `Map "${fileName}" could not be parsed and cannot be loaded.`;
  }
}

function containsMergeConflictMarkers(rawText) {
  if (typeof rawText !== 'string' || !rawText) {
    return false;
  }

  return /^<<<<<<<(?: .*)?$/m.test(rawText)
    && /^=======$/m.test(rawText)
    && /^>>>>>>> (?:.*)$/m.test(rawText);
}
