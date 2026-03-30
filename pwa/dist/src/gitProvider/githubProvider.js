import { recordSyncFailure, recordSyncSuccess } from './syncMetadata.js';
import { GitHubAdapter } from './adapters/githubAdapter.js';

export class GitHubProvider {
  constructor(config) {
    this.adapter = new GitHubAdapter(config);
  }

  async listDirectory(path) {
    try {
      const response = await this.adapter.listDirectory(path, `listing maps directory ${path || '/'}`);
      recordSyncSuccess(`Listed ${path || '/'} from GitHub.`);
      return Array.isArray(response) ? response : [];
    } catch (error) {
      recordSyncFailure(toErrorSummary('list', path || '/', error));
      throw error;
    }
  }

  async getFile(path) {
    try {
      const response = await this.adapter.getContent(path, `loading remote file ${path}`);
      recordSyncSuccess(`Loaded ${path} from GitHub.`);
      return {
        content: decodeContent(response.content, response.encoding),
        versionToken: response.sha,
      };
    } catch (error) {
      recordSyncFailure(toErrorSummary('read', path, error));
      throw error;
    }
  }

  async putFile(path, content, versionToken, message) {
    try {
      const response = await this.adapter.putContent(path, {
        message,
        content: encodeContent(content),
        sha: versionToken ?? undefined,
      }, `saving remote file ${path}`);
      recordSyncSuccess(`Saved ${path} to GitHub.`);
      return {
        versionToken: response.content?.sha ?? '',
        commitSha: response.commit?.sha,
      };
    } catch (error) {
      recordSyncFailure(toErrorSummary('write', path, error));
      throw error;
    }
  }
}

function decodeContent(content, encoding) {
  if (encoding !== 'base64') {
    throw new Error(`Unsupported GitHub content encoding: ${encoding}`);
  }

  const sanitized = content.replace(/\n/g, '');
  const binary = atob(sanitized);
  const bytes = Uint8Array.from(binary, (character) => character.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

function encodeContent(content) {
  const bytes = new TextEncoder().encode(content);
  let binary = '';
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });
  return btoa(binary);
}

function toErrorSummary(operation, path, error) {
  const message = error instanceof Error ? error.message : String(error);
  return `Sync ${operation} failed for ${path}: ${message}`;
}
