import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildTodoAddCommitMessage,
  buildTodoDeleteCommitMessage,
  buildTodoEditCommitMessage,
  buildTodoToggleCommitMessage,
} from './commitMessages.ts';

describe('commit message generation', () => {
  it('normalizes whitespace for add messages', () => {
    assert.equal(buildTodoAddCommitMessage('  ship   release   notes  '), 'todo:add ship release notes');
  });

  it('truncates add message text to 72 characters', () => {
    const text = 'x'.repeat(90);
    assert.equal(buildTodoAddCommitMessage(text), `todo:add ${'x'.repeat(72)}`);
  });

  it('builds edit/toggle/delete messages with normalized ids', () => {
    assert.equal(buildTodoEditCommitMessage('  id-1  '), 'todo:edit id-1');
    assert.equal(buildTodoToggleCommitMessage(' id-1 ', true), 'todo:toggle id-1 -> done');
    assert.equal(buildTodoToggleCommitMessage(' id-1 ', false), 'todo:toggle id-1 -> open');
    assert.equal(buildTodoDeleteCommitMessage('  id-1  '), 'todo:delete id-1');
  });
});
