(function initUpdateChecker() {
  const VERSION_CHECK_INTERVAL_MS = 10 * 60 * 1000; // 10 minutes
  let knownVersion = null;

  async function fetchVersion() {
    try {
      const res = await fetch('./version.json', { cache: 'no-store' });
      if (!res.ok) return null;
      const data = await res.json();
      return typeof data.version === 'number' ? data.version : null;
    } catch {
      return null;
    }
  }

  function showUpdateBanner() {
    const banner = document.getElementById('update-banner');
    if (!banner || !banner.hidden) return;
    banner.hidden = false;
  }

  async function checkForUpdate() {
    const latest = await fetchVersion();
    if (latest === null) return;

    if (knownVersion === null) {
      knownVersion = latest;
      return;
    }

    if (latest > knownVersion) {
      showUpdateBanner();
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    const banner = document.getElementById('update-banner');
    if (banner) {
      banner.querySelector('[data-action="reload-app"]')?.addEventListener('click', () => {
        window.location.reload();
      });
    }

    void checkForUpdate();
    setInterval(() => void checkForUpdate(), VERSION_CHECK_INTERVAL_MS);
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') {
        void checkForUpdate();
      }
    });
  });
})();

(function bootstrapFocusPwaShell() {
  function whenDomReady(callback) {
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', callback, { once: true });
      return;
    }

    callback();
  }

  function isLocalDevelopmentHost() {
    return ['localhost', '127.0.0.1', '[::1]'].includes(window.location.hostname);
  }

  async function disableLocalServiceWorkerCaching() {
    if (!('serviceWorker' in navigator)) {
      return;
    }

    try {
      const registrations = await navigator.serviceWorker.getRegistrations();
      await Promise.all(registrations.map((registration) => registration.unregister()));

      if ('caches' in window) {
        const cacheKeys = await caches.keys();
        const focusCacheKeys = cacheKeys.filter((cacheKey) => cacheKey.startsWith('focus-pwa-shell-'));
        await Promise.all(focusCacheKeys.map((cacheKey) => caches.delete(cacheKey)));
      }
    } catch (error) {
      console.warn('Failed to clear local service worker caches', error);
    }
  }

  function setBootMessage(message, detail) {
    const statusNode = document.getElementById('sync-status');
    const detailNode = document.getElementById('sync-detail');
    const screenRoot = document.getElementById('screen-root');

    if (statusNode) {
      statusNode.textContent = message;
      statusNode.dataset.tone = 'error';
    }

    if (detailNode) {
      detailNode.textContent = detail;
    }

    if (screenRoot) {
      screenRoot.hidden = false;
      screenRoot.innerHTML = `
        <section class="card connection-card" aria-label="Startup error">
          <h2>App failed to start</h2>
          <p class="card-copy">${escapeHtml(message)}</p>
          <p class="security-note">${escapeHtml(detail)}</p>
        </section>
      `;
    }
  }

  function escapeHtml(value) {
    return String(value ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  if (window.location.protocol === 'file:') {
    whenDomReady(() => {
      setBootMessage(
        'Local file mode is not supported for this PWA.',
        'Open it through http://localhost instead, for example by running pwa\\serve-local.ps1 or another static web server.',
      );
    });
    return;
  }

  whenDomReady(() => {
    Promise.resolve()
      .then(() => {
        if (isLocalDevelopmentHost()) {
          return disableLocalServiceWorkerCaching();
        }

        return undefined;
      })
      .then(() => import('./src/main.js'))
      .then(({ bootstrapApp }) => bootstrapApp())
      .catch((error) => {
        console.error('Focus PWA bootstrap failed', error);
        setBootMessage(
          'The PWA runtime could not be loaded.',
          error instanceof Error
            ? error.message
            : 'Check the browser console and verify your local server serves .js files correctly.',
        );
      });
  });
})();
