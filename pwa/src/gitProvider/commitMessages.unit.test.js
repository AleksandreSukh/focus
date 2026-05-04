import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildNodeHideDoneTasksCommitMessage,
  buildNodeStarCommitMessage,
  buildNodeTaskStateCommitMessage,
} from './commitMessages.js';

describe('mind map commit message generation', () => {
  it('builds task state commit messages for map nodes', () => {
    assert.equal(
      buildNodeTaskStateCommitMessage(' Alpha Map ', ' node-1 ', 3),
      'map:task Alpha Map node-1 -> done',
    );
  });

  it('builds hide-done commit messages for branch visibility changes', () => {
    assert.equal(
      buildNodeHideDoneTasksCommitMessage(' Alpha Map ', ' node-1 ', true),
      'map:hide-done Alpha Map node-1 -> hide',
    );
    assert.equal(
      buildNodeHideDoneTasksCommitMessage(' Alpha Map ', ' node-1 ', false),
      'map:hide-done Alpha Map node-1 -> show',
    );
  });

  it('builds star commit messages for node star changes', () => {
    assert.equal(
      buildNodeStarCommitMessage(' Alpha Map ', ' node-1 ', true),
      'map:star Alpha Map node-1 -> starred',
    );
    assert.equal(
      buildNodeStarCommitMessage(' Alpha Map ', ' node-1 ', false),
      'map:star Alpha Map node-1 -> unstarred',
    );
  });
});
