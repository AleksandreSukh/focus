import {
  GitHubAdapter,
  GitHubAdapterConfig,
  GitHubCommitResponse,
} from './adapters/githubAdapter';
import { GitProvider, GetFileResult, GitProviderWriteResult } from './types';

export class GitHubProvider implements GitProvider {
  private readonly adapter: GitHubAdapter;

  constructor(config: GitHubAdapterConfig) {
    this.adapter = new GitHubAdapter(config);
  }

  async getFile(path: string): Promise<GetFileResult> {
    const response = await this.adapter.getContent(path);
    return {
      content: decodeContent(response.content, response.encoding),
      versionToken: response.sha,
    };
  }

  async putFile(
    path: string,
    content: string,
    versionToken: string | null,
    message: string,
  ): Promise<GitProviderWriteResult> {
    const response = await this.adapter.putContent(path, {
      message,
      content: encodeContent(content),
      sha: versionToken ?? undefined,
    });

    return toWriteResult(response);
  }

  async deleteFile(
    path: string,
    versionToken: string,
    message: string,
  ): Promise<GitProviderWriteResult> {
    const response = await this.adapter.deleteContent(path, {
      message,
      sha: versionToken,
    });

    return toWriteResult(response);
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
