import { buildConflictResolveCommitMessage } from '../gitProvider/commitMessages.js';
import { GitHubApiError } from '../gitProvider/adapters/githubAdapter.js';
import { GitHubProvider } from '../gitProvider/githubProvider.js';
import {
  normalizeMindMapDocument,
  parseMindMapDocument,
  serializeMindMapDocument,
} from './model.js';
import { hasConflictMarkers, tryResolveMapConflict } from './mapConflictResolver.js';

export const UNREADABLE_MAP_REASON = Object.freeze({
  AUTO_RESOLVE_FAILED: 'autoResolveFailed',
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

    if (hasConflictMarkers(snapshot.content)) {
      return this.loadResolvedConflictMap({
        filePath,
        fileName,
        mapName,
        snapshot,
      });
    }

    try {
      return buildLoadedMapSnapshot({
        filePath,
        fileName,
        mapName,
        content: snapshot.content,
        revision: snapshot.versionToken,
      });
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

  async getAttachmentBlob(_mapFilePath, nodeId, relativePath, mediaType) {
    const attachmentPath = buildAttachmentPath(this.directoryPath, nodeId, relativePath);
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

  async loadResolvedConflictMap({ filePath, fileName, mapName, snapshot }) {
    const initialResolved = buildResolvedDocument(snapshot.content);
    if (!initialResolved.ok) {
      throw createUnreadableMapError({
        filePath,
        fileName,
        mapName,
        revision: snapshot.versionToken,
        rawText: snapshot.content,
        cause: createAutoResolveFailureError(initialResolved.cause),
      });
    }

    try {
      const saved = await this.gitProvider.putFile(
        filePath,
        initialResolved.value.serializedContent,
        snapshot.versionToken,
        buildConflictResolveCommitMessage(mapName),
      );
      return buildLoadedMapSnapshot({
        filePath,
        fileName,
        mapName,
        content: initialResolved.value.serializedContent,
        revision: saved.versionToken,
        document: initialResolved.value.document,
      });
    } catch (error) {
      if (!(error instanceof GitHubApiError) || error.code !== 'CONFLICT') {
        throw error;
      }
    }

    const latest = await this.gitProvider.getFile(filePath);
    if (!hasConflictMarkers(latest.content)) {
      try {
        return buildLoadedMapSnapshot({
          filePath,
          fileName,
          mapName,
          content: latest.content,
          revision: latest.versionToken,
        });
      } catch (cause) {
        throw createUnreadableMapError({
          filePath,
          fileName,
          mapName,
          revision: latest.versionToken,
          rawText: latest.content,
          cause,
        });
      }
    }

    const retriedResolved = buildResolvedDocument(latest.content);
    if (!retriedResolved.ok) {
      throw createUnreadableMapError({
        filePath,
        fileName,
        mapName,
        revision: latest.versionToken,
        rawText: latest.content,
        cause: createAutoResolveFailureError(retriedResolved.cause),
      });
    }

    try {
      const saved = await this.gitProvider.putFile(
        filePath,
        retriedResolved.value.serializedContent,
        latest.versionToken,
        buildConflictResolveCommitMessage(mapName),
      );
      return buildLoadedMapSnapshot({
        filePath,
        fileName,
        mapName,
        content: retriedResolved.value.serializedContent,
        revision: saved.versionToken,
        document: retriedResolved.value.document,
      });
    } catch (cause) {
      if (cause instanceof GitHubApiError && cause.code === 'CONFLICT') {
        throw createUnreadableMapError({
          filePath,
          fileName,
          mapName,
          revision: latest.versionToken,
          rawText: latest.content,
          cause: createAutoResolveFailureError(cause),
        });
      }

      throw cause;
    }
  }

  async uploadAttachment({ mapFilePath: _mapFilePath, nodeId, relativePath, base64Content, commitMessage }) {
    const attachmentPath = buildAttachmentPath(this.directoryPath, nodeId, relativePath);
    const result = await this.gitProvider.putBinaryFile(
      attachmentPath,
      base64Content,
      null,
      commitMessage,
    );
    return { ok: true, versionToken: result.versionToken };
  }

  async deleteAttachment({ mapFilePath: _mapFilePath, nodeId, relativePath, versionToken, commitMessage }) {
    const attachmentPath = buildAttachmentPath(this.directoryPath, nodeId, relativePath);
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
  if (cause?.code === 'AUTO_RESOLVE_FAILED') {
    return UNREADABLE_MAP_REASON.AUTO_RESOLVE_FAILED;
  }

  if (hasConflictMarkers(rawText)) {
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
    case UNREADABLE_MAP_REASON.AUTO_RESOLVE_FAILED:
      return `This map has merge conflicts that couldn't be auto-resolved. Repair locally or reset it from GitHub.`;
    case UNREADABLE_MAP_REASON.MERGE_CONFLICT:
      return `Map "${fileName}" contains unresolved Git merge markers and cannot be loaded.`;
    case UNREADABLE_MAP_REASON.INVALID_JSON:
      return `Map "${fileName}" is not valid JSON and cannot be loaded.`;
    default:
      return `Map "${fileName}" could not be parsed and cannot be loaded.`;
  }
}

function buildAttachmentPath(directoryPath, nodeId, relativePath) {
  const parts = [];
  if (directoryPath) {
    parts.push(directoryPath);
  }

  parts.push('_attachments', normalizeAttachmentNodeId(nodeId), normalizeAttachmentRelativePath(relativePath));
  return parts.join('/');
}

function normalizeAttachmentNodeId(nodeId) {
  return String(nodeId ?? '').trim().toLowerCase();
}

function normalizeAttachmentRelativePath(relativePath) {
  return String(relativePath ?? '').split('/').pop()?.split('\\').pop() || '';
}

function buildResolvedDocument(content) {
  const resolved = tryResolveMapConflict(content);
  if (!resolved.ok || typeof resolved.resolvedContent !== 'string' || !resolved.resolvedContent.trim()) {
    return {
      ok: false,
      cause: new Error('Map conflict resolver could not safely resolve this file.'),
    };
  }

  try {
    const document = normalizeMindMapDocument(parseMindMapDocument(resolved.resolvedContent));
    return {
      ok: true,
      value: {
        document,
        serializedContent: serializeMindMapDocument(document),
      },
    };
  } catch (cause) {
    return {
      ok: false,
      cause,
    };
  }
}

function buildLoadedMapSnapshot({
  filePath,
  fileName,
  mapName,
  content,
  revision,
  document,
}) {
  const parsedDocument = document ?? normalizeMindMapDocument(parseMindMapDocument(content));
  return {
    filePath,
    fileName,
    mapName,
    document: parsedDocument,
    revision,
    loadedAt: Date.now(),
  };
}

function createAutoResolveFailureError(cause) {
  const error = new Error('Map conflict auto-resolution failed.');
  error.code = 'AUTO_RESOLVE_FAILED';
  error.cause = cause;
  return error;
}
