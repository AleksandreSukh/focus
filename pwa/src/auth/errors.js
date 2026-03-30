const DEFAULT_AUTH_ERROR_MESSAGE =
  'Authentication failed. Verify the token and try again.';

function buildContextSuffix(contextLabel) {
  return contextLabel ? ` while ${contextLabel}` : '';
}

function buildNotFoundFallback(source, contextLabel) {
  const contextSuffix = buildContextSuffix(contextLabel);

  if (source?.operation === 'probeBranch') {
    return `Configured branch was not found (404 Not Found)${contextSuffix}. Check the branch name.`;
  }

  return `Repository was not found (404 Not Found)${contextSuffix}. Check the owner, repository name, and token access.`;
}

function buildNetworkFallback(contextLabel) {
  return `Unable to reach the GitHub API${buildContextSuffix(contextLabel)}. Check your network connection and try again.`;
}

function buildAuthError(code, status, message, source, contextLabel) {
  return {
    code,
    status,
    message,
    operation: typeof source?.operation === 'string' ? source.operation : '',
    contextLabel,
    responseText: typeof source?.responseText === 'string' ? source.responseText : '',
  };
}

export function mapAuthFailure(source, contextLabel = 'validating repository access') {
  const status = typeof source === 'number'
    ? source
    : typeof source?.status === 'number'
      ? source.status
      : undefined;
  const code = typeof source?.code === 'string' ? source.code : '';
  const sourceMessage = typeof source?.message === 'string' ? source.message : '';
  const contextSuffix = buildContextSuffix(contextLabel);

  if (code === 'RATE_LIMIT') {
    return buildAuthError(
      code,
      status,
      sourceMessage ||
        `GitHub rate limit reached${contextSuffix}. Wait a moment and try again.`,
      source,
      contextLabel,
    );
  }

  if (status === 401 || code === 'UNAUTHORIZED') {
    return buildAuthError(
      'UNAUTHORIZED',
      401,
      sourceMessage ||
        `Token was rejected (401 Unauthorized)${contextSuffix}. Please generate a new token.`,
      source,
      contextLabel,
    );
  }

  if (status === 403 || code === 'FORBIDDEN') {
    return buildAuthError(
      'FORBIDDEN',
      403,
      sourceMessage ||
        `Token is valid but lacks required repository access (403 Forbidden)${contextSuffix}. Update token permissions.`,
      source,
      contextLabel,
    );
  }

  if (status === 404 || code === 'NOT_FOUND') {
    return buildAuthError(
      'NOT_FOUND',
      404,
      sourceMessage || buildNotFoundFallback(source, contextLabel),
      source,
      contextLabel,
    );
  }

  if (code === 'NETWORK') {
    return buildAuthError(
      'NETWORK',
      undefined,
      sourceMessage || buildNetworkFallback(contextLabel),
      source,
      contextLabel,
    );
  }

  if (typeof status === 'number') {
    return buildAuthError(
      'UNKNOWN',
      status,
      sourceMessage || `${DEFAULT_AUTH_ERROR_MESSAGE}${contextSuffix} (HTTP ${status})`,
      source,
      contextLabel,
    );
  }

  return buildAuthError(
    'NETWORK',
    undefined,
    sourceMessage || buildNetworkFallback(contextLabel),
    source,
    contextLabel,
  );
}
