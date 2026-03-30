import { GitHubApiError } from '../gitProvider/adapters/githubAdapter.js';
import { GitHubProvider } from '../gitProvider/githubProvider.js';
import { recordSyncSuccess } from '../gitProvider/syncMetadata.js';

export const EMPTY_REVISION = '__focus_empty__';

const CURRENT_VERSION = 1;

export class GitHubTodoProvider {
  constructor({ owner, repo, branch, token, filePath }) {
    this.filePath = filePath;
    this.gitProvider = new GitHubProvider({
      owner,
      repo,
      branch,
      token,
    });
  }

  async load() {
    try {
      const snapshot = await this.gitProvider.getFile(this.filePath);
      return {
        document: normalizeDocument(parseDocument(snapshot.content)),
        revision: snapshot.versionToken,
        loadedAt: Date.now(),
      };
    } catch (error) {
      if (error instanceof GitHubApiError && error.code === 'NOT_FOUND') {
        recordSyncSuccess('Remote todo file not found yet. It will be created on first save.');
        return {
          document: { version: CURRENT_VERSION, items: [] },
          revision: EMPTY_REVISION,
          loadedAt: Date.now(),
        };
      }

      throw error;
    }
  }

  async save({ document, expectedRevision, commitMessage }) {
    try {
      const result = await this.gitProvider.putFile(
        this.filePath,
        serializeDocument(normalizeDocument(document)),
        expectedRevision === EMPTY_REVISION ? null : expectedRevision,
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

function parseDocument(content) {
  return JSON.parse(content);
}

function normalizeDocument(document) {
  if (!document || typeof document !== 'object' || !Array.isArray(document.items)) {
    return { version: CURRENT_VERSION, items: [] };
  }

  return {
    version: Number.isInteger(document.version) ? document.version : CURRENT_VERSION,
    items: document.items
      .filter((item) => item && typeof item.id === 'string' && typeof item.text === 'string')
      .map((item) => ({
        id: item.id,
        text: item.text,
        completed: Boolean(item.completed),
        deleted: Boolean(item.deleted),
        createdAt: typeof item.createdAt === 'string' ? item.createdAt : new Date().toISOString(),
        updatedAt: typeof item.updatedAt === 'string' ? item.updatedAt : new Date().toISOString(),
      })),
  };
}

function serializeDocument(document) {
  return `${JSON.stringify(document, null, 2)}\n`;
}
