(function initFocusRuntimeConfig() {
  const locationPath = window.location.pathname;
  const pathParts = locationPath.split('/').filter(Boolean);
  const inferredRepo = pathParts.length > 0 ? pathParts[0] : '';

  window.__FOCUS_RUNTIME_CONFIG__ = {
    host: 'github-pages',
    repoOwner: '',
    repoName: inferredRepo,
    repoBranch: 'main',
    repoPath: '/',
    auth: {
      tokenStorageKey: 'focus_runtime_token',
      tokenSource: 'runtime-only',
    },
  };
})();
