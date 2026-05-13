import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  canGoBack,
  canGoForward,
  createNavigationHistory,
  goBack,
  goForward,
  navigationEntriesEqual,
  normalizeNavigationEntry,
  normalizeNavigationHistory,
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
    const status = { ...base, overlay: { kind: 'status' } };
    const settings = { ...base, overlay: { kind: 'settings' } };

    assert.equal(navigationEntriesEqual(base, status), false);
    assert.equal(navigationEntriesEqual(status, { ...status }), true);

    let history = createNavigationHistory(base);
    history = pushNavigationEntry(history, status);
    history = pushNavigationEntry(history, settings);

    assert.equal(history.backStack.length, 2);
    assert.deepEqual(history.current.overlay, { kind: 'settings' });
  });

  it('skips edit node overlays when normalizing entries', () => {
    const base = { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'node' };
    const entry = normalizeNavigationEntry({
      ...base,
      overlay: { kind: 'editNode', mapPath: 'FocusMaps/A.json', nodeId: 'node' },
    });

    assert.deepEqual(entry, {
      ...base,
      overlay: null,
    });
  });

  it('does not push edit node modal state into navigation history', () => {
    const base = { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'node' };
    let history = createNavigationHistory(base);

    history = pushNavigationEntry(history, {
      ...base,
      overlay: { kind: 'editNode', mapPath: 'FocusMaps/A.json', nodeId: 'node' },
    });

    assert.equal(canGoBack(history), false);
    assert.equal(history.backStack.length, 0);
    assert.deepEqual(history.current, {
      ...base,
      overlay: null,
    });
  });

  it('prunes old edit node modal entries from normalized stacks', () => {
    const base = { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'node', overlay: null };
    const other = { view: 'map', mapPath: 'FocusMaps/B.json', nodeId: 'root', overlay: null };
    const edit = {
      ...base,
      overlay: { kind: 'editNode', mapPath: 'FocusMaps/A.json', nodeId: 'node' },
    };
    const status = { ...base, overlay: { kind: 'status' } };

    const history = normalizeNavigationHistory({
      current: base,
      backStack: [other, edit],
      forwardStack: [edit, status],
    });

    assert.deepEqual(history.current, base);
    assert.deepEqual(history.backStack, [other]);
    assert.deepEqual(history.forwardStack, [status]);

    let navigated = goBack(history);
    assert.deepEqual(navigated.current, other);
    navigated = goForward(navigated);

    assert.deepEqual(navigated.current, base);
    assert.deepEqual(navigated.forwardStack, [status]);
  });

  it('keeps redo clearing behavior after pruning edit node entries', () => {
    const base = { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'node' };
    const status = { ...base, overlay: { kind: 'status' } };
    let history = createNavigationHistory(base);

    history = pushNavigationEntry(history, { view: 'tasks' });
    history = goBack(history);
    history = pushNavigationEntry(history, {
      ...base,
      overlay: { kind: 'editNode', mapPath: 'FocusMaps/A.json', nodeId: 'node' },
    });
    history = pushNavigationEntry(history, status);

    assert.equal(canGoForward(history), false);
    assert.equal(history.backStack.length, 1);
    assert.deepEqual(history.current.overlay, { kind: 'status' });
  });
});
