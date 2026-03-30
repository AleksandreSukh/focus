import {
  GitHubAdapter,
  GitHubAdapterConfig,
  GitHubCommitResponse,
} from './adapters/githubAdapter';
import { GitProvider, GetFileResult, GitProviderWriteResult } from './types';
import { recordSyncFailure, recordSyncSuccess } from './syncMetadata';

export class GitHubProvider implements GitProvider {
  private readonly adapter: GitHubAdapter;

  constructor(config: GitHubAdapterConfig) {
    this.adapter = new GitHubAdapter(config);
  }

  async getFile(path: string): Promise<GetFileResult> {
    try {
      const response = await this.adapter.getContent(path);
      recordSyncSuccess();
      return {
        content: decodeContent(response.content, response.encoding),
        versionToken: response.sha,
      };
    } catch (error) {
      recordSyncFailure(toErrorSummary('read', path, error));
      throw error;
    }
  }

  async putFile(
    path: string,
    content: string,
    versionToken: string | null,
    message: string,
  ): Promise<GitProviderWriteResult> {
    try {
      const response = await this.adapter.putContent(path, {
        message,
        content: encodeContent(content),
        sha: versionToken ?? undefined,
      });
      recordSyncSuccess();
      return toWriteResult(response);
    } catch (error) {
      recordSyncFailure(toErrorSummary('write', path, error));
      throw error;
    }
  }

  async deleteFile(
    path: string,
    versionToken: string,
    message: string,
  ): Promise<GitProviderWriteResult> {
    try {
      const response = await this.adapter.deleteContent(path, {
        message,
        sha: versionToken,
      });
      recordSyncSuccess();
      return toWriteResult(response);
    } catch (error) {
      recordSyncFailure(toErrorSummary('delete', path, error));
      throw error;
    }
  }
}

function toWriteResult(response: GitHubCommitResponse): GitProviderWriteResult {
  return {
    versionToken: response.content?.sha ?? '',
    commitSha: response.commit?.sha,
  };
}

function decodeContent(content: string, encoding: string): string {
  if (encoding !== 'base64') {
    throw new Error(`Unsupported GitHub content encoding: ${encoding}`);
  }

  const sanitized = content.replace(/\n/g, '');
  const bufferApi = (globalThis as { Buffer?: { from: (input: string, format: string) => { toString: (encoding: string) => string } } }).Buffer;

  if (bufferApi) {
    return bufferApi.from(sanitized, 'base64').toString('utf-8');
  }

  const binary = atob(sanitized);
  const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

function encodeContent(content: string): string {
  const bufferApi = (globalThis as { Buffer?: { from: (input: string, format: string) => { toString: (encoding: string) => string } } }).Buffer;

  if (bufferApi) {
    return bufferApi.from(content, 'utf-8').toString('base64');
  }

  const bytes = new TextEncoder().encode(content);
  let binary = '';
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });

  return btoa(binary);
}

function toErrorSummary(operation: 'read' | 'write' | 'delete', path: string, error: unknown): string {
  const message = error instanceof Error ? error.message : String(error);
  return `Sync ${operation} failed for ${path}: ${message}`;
}
