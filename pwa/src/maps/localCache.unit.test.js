import assert from 'node:assert/strict';
import { afterEach, describe, it } from 'node:test';
import {
  loadCachedWorkspaceInitialized,
  saveCachedWorkspaceInitialized,
} from './localCache.js';

const originalWindow = globalThis.window;

function installLocalStorage() {
  const values = new Map();
  globalThis.window = {
    localStorage: {
      getItem(key) {
        return values.has(key) ? values.get(key) : null;
      },
      setItem(key, value) {
        values.set(key, String(value));
      },
      removeItem(key) {
        values.delete(key);
      },
    },
  };
}

afterEach(() => {
  globalThis.window = originalWindow;
});

describe('map local cache workspace marker', () => {
  it('persists and clears repo-scoped initialized state', () => {
    installLocalStorage();

    assert.equal(loadCachedWorkspaceInitialized('owner::repo::main::FocusMaps'), false);

    saveCachedWorkspaceInitialized('owner::repo::main::FocusMaps', true);

    assert.equal(loadCachedWorkspaceInitialized('owner::repo::main::FocusMaps'), true);
    assert.equal(loadCachedWorkspaceInitialized('owner::repo::dev::FocusMaps'), false);

    saveCachedWorkspaceInitialized('owner::repo::main::FocusMaps', false);

    assert.equal(loadCachedWorkspaceInitialized('owner::repo::main::FocusMaps'), false);
  });
});
