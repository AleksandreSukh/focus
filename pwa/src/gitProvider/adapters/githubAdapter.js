const DEFAULT_GITHUB_API_BASE_URL = 'https://api.github.com';

export class GitHubApiError extends Error {
  constructor({
    code,
    status,
    statusText,
    message,
    responseText = '',
    retryAfter = null,
    rateLimitResetAt = null,
  }) {
    super(message);
    this.name = 'GitHubApiError';
    this.code = code;
    this.status = status;
    this.statusText = statusText;
    this.responseText = responseText;
    this.retryAfter = retryAfter;
    this.rateLimitResetAt = rateLimitResetAt;
  }
}

export class GitHubAdapter {
  constructor(config) {
    this.owner = config.owner;
    this.repo = config.repo;
    this.token = config.token;
    this.branch = config.branch;
    this.apiBaseUrl = config.apiBaseUrl ?? DEFAULT_GITHUB_API_BASE_URL;
    this.fetchImpl = config.fetchImpl ?? fetch;
  }

  async probeRepository() {
    return this.requestJson(`/repos/${this.owner}/${this.repo}`, {
      method: 'GET',
    });
  }

  async getContent(path) {
    const query = this.branch ? `?ref=${encodeURIComponent(this.branch)}` : '';
    return this.requestJson(
      `/repos/${this.owner}/${this.repo}/contents/${normalizePath(path)}${query}`,
      {
        method: 'GET',
      },
    );
  }

  async putContent(path, payload) {
    return this.requestJson(
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

  async requestJson(endpoint, init) {
    let response;

    try {
      response = await this.fetchImpl(`${this.apiBaseUrl}${endpoint}`, {
        ...init,
        headers: {
          Accept: 'application/vnd.github+json',
          Authorization: `Bearer ${this.token}`,
          'X-GitHub-Api-Version': '2022-11-28',
          ...(init.body ? { 'Content-Type': 'application/json' } : {}),
        },
      });
    } catch (error) {
      throw new GitHubApiError({
        code: 'NETWORK',
        status: null,
        statusText: 'Network failure',
        message:
          'Unable to reach the GitHub API. Check your network connection and try again.',
        responseText: error instanceof Error ? error.message : String(error),
      });
    }

    if (!response.ok) {
      const responseText = await response.text();
      throw createGitHubApiError(response, responseText);
    }

    return response.json();
  }
}

function normalizePath(path) {
  return String(path ?? '')
    .split('/')
    .filter(Boolean)
    .map(encodeURIComponent)
    .join('/');
}

function createGitHubApiError(response, responseText) {
  const retryAfterHeader = response.headers.get('retry-after');
  const retryAfter = retryAfterHeader ? Number.parseInt(retryAfterHeader, 10) : null;
  const rateLimitResetHeader = response.headers.get('x-ratelimit-reset');
  const rateLimitResetAt = rateLimitResetHeader
    ? new Date(Number.parseInt(rateLimitResetHeader, 10) * 1000).toISOString()
    : null;
  const code = classifyGitHubError(response, responseText);

  return new GitHubApiError({
    code,
    status: response.status,
    statusText: response.statusText,
    message: buildErrorMessage(code, response.status, responseText, retryAfter, rateLimitResetAt),
    responseText,
    retryAfter: Number.isFinite(retryAfter) ? retryAfter : null,
    rateLimitResetAt,
  });
}

function classifyGitHubError(response, responseText) {
  if (response.status === 401) {
    return 'UNAUTHORIZED';
  }

  if (response.status === 404) {
    return 'NOT_FOUND';
  }

  if (response.status === 409 || response.status === 422) {
    return 'CONFLICT';
  }

  if (
    response.status === 403 &&
    (
      response.headers.get('x-ratelimit-remaining') === '0' ||
      response.headers.has('retry-after') ||
      /rate limit/i.test(responseText)
    )
  ) {
    return 'RATE_LIMIT';
  }

  if (response.status === 403) {
    return 'FORBIDDEN';
  }

  return 'UNKNOWN';
}

function buildErrorMessage(code, status, responseText, retryAfter, rateLimitResetAt) {
  switch (code) {
    case 'UNAUTHORIZED':
      return 'Token was rejected (401 Unauthorized).';
    case 'FORBIDDEN':
      return 'Token is valid but lacks required repository access (403 Forbidden).';
    case 'NOT_FOUND':
      return 'Repository or configured branch/path was not found (404 Not Found).';
    case 'CONFLICT':
      return 'Remote todo file changed during sync and must be refreshed.';
    case 'RATE_LIMIT':
      return retryAfter
        ? `GitHub rate limit reached. Retry in about ${retryAfter} seconds.`
        : rateLimitResetAt
          ? `GitHub rate limit reached. Retry after ${new Date(rateLimitResetAt).toLocaleTimeString()}.`
          : 'GitHub rate limit reached. Wait a moment and try again.';
    default:
      return responseText?.trim()
        ? `GitHub API request failed (HTTP ${status}): ${truncate(responseText.trim(), 220)}`
        : `GitHub API request failed (HTTP ${status}).`;
  }
}

function truncate(value, maxLength) {
  return value.length > maxLength ? `${value.slice(0, maxLength - 1)}…` : value;
}
