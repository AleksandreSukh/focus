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

const DIALOG_OVERLAY_KINDS = [
  'addChildNote',
  'addChildTask',
  'askAi',
  'audioViewer',
  'createMap',
  'deleteAttachment',
  'deleteMap',
  'deleteNode',
  'editNode',
  'imageViewer',
  'repairMap',
  'resolveConflict',
  'settings',
  'status',
  'textViewer',
];

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

  it('includes non-dialog overlays in entry equality', () => {
    const base = { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'node' };
    const inspector = { ...base, overlay: { kind: 'inspector' } };
    const outline = { ...base, overlay: { kind: 'outline' } };

    assert.equal(navigationEntriesEqual(base, inspector), false);
    assert.equal(navigationEntriesEqual(inspector, { ...inspector }), true);

    let history = createNavigationHistory(base);
    history = pushNavigationEntry(history, inspector);
    history = pushNavigationEntry(history, outline);

    assert.equal(history.backStack.length, 2);
    assert.deepEqual(history.current.overlay, { kind: 'outline' });
  });

  it('skips dialog overlays when normalizing entries', () => {
    const base = { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'node' };

    DIALOG_OVERLAY_KINDS.forEach((kind) => {
      const entry = normalizeNavigationEntry({
        ...base,
        overlay: {
          kind,
          mapPath: 'FocusMaps/A.json',
          nodeId: 'node',
          attachmentId: 'attachment',
          attachmentRelativePath: 'note.txt',
          filePath: 'FocusMaps/A.json',
          previousOverlay: { kind: 'imageViewer', mapPath: 'FocusMaps/A.json', nodeId: 'node' },
        },
      });

      assert.deepEqual(entry, {
        ...base,
        overlay: null,
      });
    });
  });

  it('does not push dialog state into navigation history', () => {
    const base = { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'node' };
    let history = createNavigationHistory(base);

    DIALOG_OVERLAY_KINDS.forEach((kind) => {
      history = pushNavigationEntry(history, {
        ...base,
        overlay: { kind, mapPath: 'FocusMaps/A.json', nodeId: 'node' },
      });
    });

    assert.equal(canGoBack(history), false);
    assert.equal(history.backStack.length, 0);
    assert.deepEqual(history.current, {
      ...base,
      overlay: null,
    });
  });

  it('prunes old dialog entries from normalized stacks', () => {
    const base = { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'node', overlay: null };
    const other = { view: 'map', mapPath: 'FocusMaps/B.json', nodeId: 'root', overlay: null };
    const editNode = {
      ...base,
      overlay: { kind: 'editNode', mapPath: 'FocusMaps/A.json', nodeId: 'node' },
    };
    const status = { ...base, overlay: { kind: 'status' } };
    const createMap = { view: 'maps', mapPath: '', nodeId: '', overlay: { kind: 'createMap' } };
    const tasks = { view: 'tasks', mapPath: '', nodeId: '', overlay: null };

    const history = normalizeNavigationHistory({
      current: base,
      backStack: [other, editNode, status],
      forwardStack: [status, editNode, tasks, createMap],
    });

    assert.deepEqual(history.current, base);
    assert.deepEqual(history.backStack, [other]);
    assert.deepEqual(history.forwardStack, [tasks, { ...createMap, overlay: null }]);

    let navigated = goBack(history);
    assert.deepEqual(navigated.current, other);
    navigated = goForward(navigated);

    assert.deepEqual(navigated.current, base);
    assert.deepEqual(navigated.forwardStack, [tasks, { ...createMap, overlay: null }]);
  });

  it('preserves redo history when opening a dialog after going back', () => {
    const base = { view: 'map', mapPath: 'FocusMaps/A.json', nodeId: 'node' };
    let history = createNavigationHistory(base);

    history = pushNavigationEntry(history, { view: 'tasks' });
    history = goBack(history);

    assert.equal(canGoForward(history), true);

    history = pushNavigationEntry(history, {
      ...base,
      overlay: { kind: 'createMap' },
    });

    assert.equal(canGoForward(history), true);
    assert.equal(history.backStack.length, 0);
    assert.deepEqual(history.forwardStack, [{
      view: 'tasks',
      mapPath: '',
      nodeId: '',
      overlay: null,
    }]);
  });
});
