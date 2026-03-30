const DEFAULT_GITHUB_API_BASE_URL = 'https://api.github.com';

export class GitHubApiError extends Error {
  constructor({
    code,
    status,
    statusText,
    message,
    operation = '',
    contextLabel = '',
    responseText = '',
    retryAfter = null,
    rateLimitResetAt = null,
  }) {
    super(message);
    this.name = 'GitHubApiError';
    this.code = code;
    this.status = status;
    this.statusText = statusText;
    this.operation = operation;
    this.contextLabel = contextLabel;
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
    this.fetchImpl = resolveFetchImplementation(config.fetchImpl);
  }

  async probeRepository(contextLabel = 'validating repository access') {
    return this.requestJson(
      `/repos/${this.owner}/${this.repo}`,
      {
        method: 'GET',
      },
      { operation: 'probeRepository', contextLabel },
    );
  }

  async probeBranch(branch, contextLabel = `validating branch "${branch}"`) {
    return this.requestJson(
      `/repos/${this.owner}/${this.repo}/branches/${encodeURIComponent(branch)}`,
      {
        method: 'GET',
      },
      { operation: 'probeBranch', contextLabel },
    );
  }

  async getContent(path, contextLabel = `loading ${path}`) {
    const query = this.branch ? `?ref=${encodeURIComponent(this.branch)}` : '';
    return this.requestJson(
      `/repos/${this.owner}/${this.repo}/contents/${normalizePath(path)}${query}`,
      {
        method: 'GET',
      },
      { operation: 'getContent', contextLabel },
    );
  }

  async listDirectory(path, contextLabel = `listing ${path || 'repository root'}`) {
    const query = this.branch ? `?ref=${encodeURIComponent(this.branch)}` : '';
    const normalizedPath = normalizePath(path);
    const contentPath = normalizedPath ? `/contents/${normalizedPath}` : '/contents';
    return this.requestJson(
      `/repos/${this.owner}/${this.repo}${contentPath}${query}`,
      {
        method: 'GET',
      },
      { operation: 'listDirectory', contextLabel },
    );
  }

  async putContent(path, payload, contextLabel = `saving ${path}`) {
    return this.requestJson(
      `/repos/${this.owner}/${this.repo}/contents/${normalizePath(path)}`,
      {
        method: 'PUT',
        body: JSON.stringify({
          ...payload,
          branch: this.branch,
        }),
      },
      { operation: 'putContent', contextLabel },
    );
  }

  async requestJson(endpoint, init, context = {}) {
    let response;
    const operation = context.operation || '';
    const contextLabel = context.contextLabel || defaultContextLabel(operation);

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
        operation,
        contextLabel,
        message: buildNetworkErrorMessage(operation, contextLabel),
        responseText: error instanceof Error ? error.message : String(error),
      });
    }

    if (!response.ok) {
      const responseText = await response.text();
      throw createGitHubApiError(response, responseText, context);
    }

    return response.json();
  }
}

function resolveFetchImplementation(fetchImpl) {
  const candidate = fetchImpl ?? globalThis.fetch;
  if (typeof candidate !== 'function') {
    throw new Error('Fetch API is unavailable in this browser environment.');
  }

  return (input, init) => candidate.call(globalThis, input, init);
}

function normalizePath(path) {
  return String(path ?? '')
    .split('/')
    .filter(Boolean)
    .map(encodeURIComponent)
    .join('/');
}

function createGitHubApiError(response, responseText, context = {}) {
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
    operation: context.operation || '',
    contextLabel: context.contextLabel || defaultContextLabel(context.operation || ''),
    message: buildErrorMessage(
      code,
      response.status,
      responseText,
      retryAfter,
      rateLimitResetAt,
      context.operation || '',
      context.contextLabel || defaultContextLabel(context.operation || ''),
    ),
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

function defaultContextLabel(operation) {
  switch (operation) {
    case 'probeRepository':
      return 'validating repository access';
    case 'probeBranch':
      return 'validating branch access';
    case 'getContent':
      return 'loading the remote file';
    case 'listDirectory':
      return 'listing the configured maps directory';
    case 'putContent':
      return 'saving the remote file';
    default:
      return 'calling the GitHub API';
  }
}

function buildNetworkErrorMessage(operation, contextLabel) {
  const label = contextLabel || defaultContextLabel(operation);
  return `Unable to reach the GitHub API while ${label}. Check your network connection and try again.`;
}

function buildNotFoundMessage(operation, contextLabel) {
  const contextSuffix = contextLabel ? ` while ${contextLabel}` : '';

  switch (operation) {
    case 'probeRepository':
      return `Repository was not found (404 Not Found)${contextSuffix}. Check the owner, repository name, and token access.`;
    case 'probeBranch':
      return `Configured branch was not found (404 Not Found)${contextSuffix}. Check the branch name.`;
    case 'getContent':
      return `Remote file was not found (404 Not Found)${contextSuffix}. Check the configured path and file name.`;
    case 'listDirectory':
      return `Configured maps directory was not found (404 Not Found)${contextSuffix}. Check the FocusMaps path in settings.`;
    case 'putContent':
      return `Remote file could not be saved because the configured branch or path was not found (404 Not Found)${contextSuffix}.`;
    default:
      return `GitHub resource was not found (404 Not Found)${contextSuffix}.`;
  }
}

function buildErrorMessage(code, status, responseText, retryAfter, rateLimitResetAt, operation, contextLabel) {
  const contextSuffix = contextLabel ? ` while ${contextLabel}` : '';

  switch (code) {
    case 'UNAUTHORIZED':
      return `Token was rejected (401 Unauthorized)${contextSuffix}.`;
    case 'FORBIDDEN':
      return `Token is valid but lacks required repository access (403 Forbidden)${contextSuffix}.`;
    case 'NOT_FOUND':
      return buildNotFoundMessage(operation, contextLabel);
    case 'CONFLICT':
      return `Remote file changed during sync${contextSuffix} and must be refreshed.`;
    case 'RATE_LIMIT':
      return retryAfter
        ? `GitHub rate limit reached${contextSuffix}. Retry in about ${retryAfter} seconds.`
        : rateLimitResetAt
          ? `GitHub rate limit reached${contextSuffix}. Retry after ${new Date(rateLimitResetAt).toLocaleTimeString()}.`
          : `GitHub rate limit reached${contextSuffix}. Wait a moment and try again.`;
    default:
      return responseText?.trim()
        ? `GitHub API request failed${contextSuffix} (HTTP ${status}): ${truncate(responseText.trim(), 220)}`
        : `GitHub API request failed${contextSuffix} (HTTP ${status}).`;
  }
}

function truncate(value, maxLength) {
  return value.length > maxLength ? `${value.slice(0, maxLength - 3)}...` : value;
}
