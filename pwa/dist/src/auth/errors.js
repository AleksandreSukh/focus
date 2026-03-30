const DEFAULT_AUTH_ERROR_MESSAGE =
  'Authentication failed. Verify the token and try again.';

export function mapAuthFailure(source) {
  const status = typeof source === 'number'
    ? source
    : typeof source?.status === 'number'
      ? source.status
      : undefined;
  const code = typeof source?.code === 'string' ? source.code : '';

  if (code === 'RATE_LIMIT') {
    return {
      code,
      status,
      message: source.message || 'GitHub rate limit reached. Wait a moment and try again.',
    };
  }

  if (status === 401 || code === 'UNAUTHORIZED') {
    return {
      code: 'UNAUTHORIZED',
      status: 401,
      message: 'Token was rejected (401 Unauthorized). Please generate a new token.',
    };
  }

  if (status === 403 || code === 'FORBIDDEN') {
    return {
      code: 'FORBIDDEN',
      status: 403,
      message:
        'Token is valid but lacks required repository access (403 Forbidden). Update token permissions.',
    };
  }

  if (status === 404 || code === 'NOT_FOUND') {
    return {
      code: 'NOT_FOUND',
      status: 404,
      message:
        'Repository not found (404 Not Found). Check the owner, repository name, branch, and path.',
    };
  }

  if (code === 'NETWORK') {
    return {
      code: 'NETWORK',
      message:
        'Unable to reach the GitHub API. Check your network connection and try again.',
    };
  }

  if (typeof status === 'number') {
    return {
      code: 'UNKNOWN',
      status,
      message: `${DEFAULT_AUTH_ERROR_MESSAGE} (HTTP ${status})`,
    };
  }

  return {
    code: 'NETWORK',
    message:
      'Unable to reach the GitHub API. Check your network connection and try again.',
  };
}
