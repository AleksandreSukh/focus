const DEFAULT_GITHUB_API_BASE_URL = 'https://api.github.com';

export type GitHubAdapterConfig = {
  owner: string;
  repo: string;
  token: string;
  branch?: string;
  apiBaseUrl?: string;
  fetchImpl?: typeof fetch;
};

export type GitHubContentFileResponse = {
  sha: string;
  content: string;
  encoding: string;
};

export type GitHubCommitResponse = {
  content?: {
    sha: string;
  };
  commit?: {
    sha: string;
  };
};

export class GitHubAdapter {
  private readonly owner: string;
  private readonly repo: string;
  private readonly token: string;
  private readonly branch?: string;
  private readonly apiBaseUrl: string;
  private readonly fetchImpl: typeof fetch;

  constructor(config: GitHubAdapterConfig) {
    this.owner = config.owner;
    this.repo = config.repo;
    this.token = config.token;
    this.branch = config.branch;
    this.apiBaseUrl = config.apiBaseUrl ?? DEFAULT_GITHUB_API_BASE_URL;
    this.fetchImpl = config.fetchImpl ?? fetch;
  }

  getBranch(): string | undefined {
    return this.branch;
  }

  async getContent(path: string): Promise<GitHubContentFileResponse> {
    const query = this.branch ? `?ref=${encodeURIComponent(this.branch)}` : '';
    return this.requestJson<GitHubContentFileResponse>(
      `/repos/${this.owner}/${this.repo}/contents/${normalizePath(path)}${query}`,
      {
        method: 'GET',
      },
    );
  }

  async putContent(
    path: string,
    payload: {
      message: string;
      content: string;
      sha?: string;
    },
  ): Promise<GitHubCommitResponse> {
    return this.requestJson<GitHubCommitResponse>(
      `/repos/${this.owner}/${this.repo}/contents/${normalizePath(path)}`,
      {
        method: 'PUT',
        body: JSON.stringify({
          ...payload,
          branch: this.branch,
        }),
      },
    );
  }

  async deleteContent(
    path: string,
    payload: {
      message: string;
      sha: string;
    },
  ): Promise<GitHubCommitResponse> {
    return this.requestJson<GitHubCommitResponse>(
      `/repos/${this.owner}/${this.repo}/contents/${normalizePath(path)}`,
      {
        method: 'DELETE',
        body: JSON.stringify({
          ...payload,
          branch: this.branch,
        }),
      },
    );
  }

  private async requestJson<T>(
    endpoint: string,
    init: RequestInit,
  ): Promise<T> {
    const response = await this.fetchImpl(`${this.apiBaseUrl}${endpoint}`, {
      ...init,
      headers: {
        Accept: 'application/vnd.github+json',
        Authorization: `Bearer ${this.token}`,
        'X-GitHub-Api-Version': '2022-11-28',
        ...(init.body ? { 'Content-Type': 'application/json' } : {}),
      },
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(
        `GitHub API error (${response.status} ${response.statusText}): ${errorText}`,
      );
    }

    return (await response.json()) as T;
  }
}

function normalizePath(path: string): string {
  return path
    .split('/')
    .filter(Boolean)
    .map(encodeURIComponent)
    .join('/');
}
