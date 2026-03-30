import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { mergeDocuments, mergeTodo } from './merge.ts';
import { Todo, TodosDocument } from './types.ts';

const todo = (overrides: Partial<Todo>): Todo => ({
  id: overrides.id ?? 'todo-1',
  text: overrides.text ?? 'Item',
  completed: overrides.completed ?? false,
  deleted: overrides.deleted ?? false,
  createdAt: overrides.createdAt ?? '2026-01-01T00:00:00.000Z',
  updatedAt: overrides.updatedAt ?? '2026-01-01T00:00:00.000Z',
});

describe('merge/conflict logic', () => {
  it('prefers the newest update when remote and local conflict', () => {
    const remote = todo({ text: 'Remote', updatedAt: '2026-01-01T00:00:01.000Z' });
    const local = todo({ text: 'Local', updatedAt: '2026-01-01T00:00:00.000Z' });

    const merged = mergeTodo(remote, local);

    assert.equal(merged.text, 'Remote');
    assert.equal(merged.updatedAt, remote.updatedAt);
  });

  it('keeps local changes when local is newer', () => {
    const remote = todo({ text: 'Remote', updatedAt: '2026-01-01T00:00:00.000Z' });
    const local = todo({ text: 'Local', updatedAt: '2026-01-01T00:00:02.000Z' });

    const merged = mergeTodo(remote, local);

    assert.equal(merged.text, 'Local');
    assert.equal(merged.updatedAt, local.updatedAt);
  });

  it('merges document sets and sorts by updatedAt descending', () => {
    const remote: TodosDocument = {
      version: 2,
      items: [
        todo({ id: 'a', text: 'Remote A', updatedAt: '2026-01-01T00:00:03.000Z' }),
        todo({ id: 'c', text: 'Remote C', updatedAt: '2026-01-01T00:00:01.000Z' }),
      ],
    };
    const local: TodosDocument = {
      version: 3,
      items: [
        todo({ id: 'a', text: 'Local A', updatedAt: '2026-01-01T00:00:02.000Z' }),
        todo({ id: 'b', text: 'Local B', updatedAt: '2026-01-01T00:00:04.000Z' }),
      ],
    };

    const merged = mergeDocuments(remote, local);

    assert.equal(merged.version, 3);
    assert.deepEqual(merged.items.map((item) => item.id), ['b', 'a', 'c']);
    assert.equal(merged.items.find((item) => item.id === 'a')?.text, 'Remote A');
  });
});
