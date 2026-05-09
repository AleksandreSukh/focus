import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  canGoBack,
  canGoForward,
  createNavigationHistory,
  goBack,
  goForward,
  navigationEntriesEqual,
  pushNavigationEntry,
  replaceNavigationEntry,
} from './history.js';

describe('navigation history', () => {
  it('pushes, goes back, and goes forward', () => {
    let history = createNavigationHistory({ view: 'maps' });
    history = pushNavigationEntry(history, { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'root' });
    history = pushNavigationEntry(history, { view: 'tasks' });

    assert.equal(canGoBack(history), true);
    assert.equal(canGoForward(history), false);

    history = goBack(history);
    assert.deepEqual(history.current, {
      view: 'map',
      mapPath: 'FocusMaps/A.json',
      nodeId: 'root',
      overlay: null,
    });
    assert.equal(canGoForward(history), true);

    history = goForward(history);
    assert.equal(history.current.view, 'tasks');
  });

  it('clears redo history when pushing after back', () => {
    let history = createNavigationHistory({ view: 'maps' });
    history = pushNavigationEntry(history, { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'root' });
    history = pushNavigationEntry(history, { view: 'tasks' });
    history = goBack(history);

    assert.equal(canGoForward(history), true);

    history = pushNavigationEntry(history, { view: 'map', mapPath: 'FocusMaps/B.json', nodeId: 'root' });

    assert.equal(history.current.mapPath, 'FocusMaps/B.json');
    assert.equal(canGoForward(history), false);
  });

  it('replaces current without adding a back entry', () => {
    let history = createNavigationHistory({ view: 'maps' });
    history = replaceNavigationEntry(history, { view: 'tasks' });

    assert.equal(history.current.view, 'tasks');
    assert.equal(canGoBack(history), false);
  });

  it('does not duplicate identical current entries', () => {
    let history = createNavigationHistory({ view: 'maps' });
    history = pushNavigationEntry(history, { view: 'maps' });

    assert.equal(history.backStack.length, 0);
  });

  it('includes overlays in entry equality', () => {
    const base = { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'node' };
    const edit = { ...base, overlay: { kind: 'editNode', mapPath: 'FocusMaps/A.json', nodeId: 'node' } };
    const status = { ...base, overlay: { kind: 'status' } };

    assert.equal(navigationEntriesEqual(base, edit), false);
    assert.equal(navigationEntriesEqual(edit, { ...edit }), true);

    let history = createNavigationHistory(base);
    history = pushNavigationEntry(history, edit);
    history = pushNavigationEntry(history, status);

    assert.equal(history.backStack.length, 2);
    assert.deepEqual(history.current.overlay, { kind: 'status' });
  });
});
