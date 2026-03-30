const CACHE_NAME = 'focus-pwa-shell-v3';
const APP_SHELL_ASSETS = [
  './',
  './index.html',
  './styles.css',
  './app.js',
  './runtime-config.js',
  './manifest.webmanifest',
  './icons/icon.svg',
  './icons/icon-maskable.svg',
  './src/main.js',
  './src/auth/errors.js',
  './src/auth/index.js',
  './src/auth/sessionManager.js',
  './src/auth/tokenValidation.js',
  './src/auth/TokenEntryScreen.js',
  './src/settings/ConnectionScreen.js',
  './src/settings/repoSettings.js',
  './src/settings/SettingsScreen.js',
  './src/gitProvider/index.js',
  './src/gitProvider/commitMessages.js',
  './src/gitProvider/githubProvider.js',
  './src/gitProvider/syncMetadata.js',
  './src/gitProvider/adapters/githubAdapter.js',
  './src/todos/githubTodoProvider.js',
  './src/todos/localCache.js',
  './src/todos/merge.js',
  './src/todos/todoRepository.js',
  './src/todos/todoService.js',
];

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE_NAME).then((cache) => cache.addAll(APP_SHELL_ASSETS)));
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') {
    return;
  }

  event.respondWith(
    caches.match(event.request).then((cached) => {
      if (cached) {
        return cached;
      }

      return fetch(event.request)
        .then((response) => {
          const copy = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(event.request, copy));
          return response;
        })
        .catch(() => caches.match('./index.html'));
    }),
  );
});
