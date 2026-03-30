import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { TodoRepository } from './todoRepository.ts';
import { TodoService } from './todoService.ts';
import { SaveRequest, SaveOutcome, TodoProvider, TodosSnapshot } from './types.ts';

class InMemoryProvider implements TodoProvider {
  private snapshot: TodosSnapshot;
  private revisionCounter = 1;

  constructor(initial: TodosSnapshot) {
    this.snapshot = structuredClone(initial);
  }

  async load(): Promise<TodosSnapshot> {
    return structuredClone(this.snapshot);
  }

  async save(request: SaveRequest): Promise<SaveOutcome> {
    if (request.expectedRevision !== this.snapshot.revision) {
      return { ok: false, reason: 'conflict', revision: this.snapshot.revision };
    }

    const revision = `rev-${++this.revisionCounter}`;
    this.snapshot = {
      document: structuredClone(request.document),
      revision,
      loadedAt: Date.now(),
    };

    return { ok: true, revision };
  }
}

const makeService = () => {
  const provider = new InMemoryProvider({
    document: { version: 1, items: [] },
    revision: 'rev-1',
    loadedAt: Date.now(),
  });
  const repository = new TodoRepository(provider);
  return new TodoService(repository);
};

describe('TodoService mutation rules', () => {
  it('rejects add when todo text is empty', async () => {
    const service = makeService();
    const result = await service.add('   ', 'todo:add blank');

    assert.equal(result.ok, false);
    if (!result.ok) {
      assert.equal(result.error.code, 'VALIDATION_ERROR');
      assert.deepEqual(result.error.meta, {
        action: 'add',
        commitMessage: 'todo:add blank',
      });
    }
  });

  it('rejects add when todo text exceeds maximum length', async () => {
    const service = makeService();
    const result = await service.add('x'.repeat(501), 'todo:add long');

    assert.equal(result.ok, false);
    if (!result.ok) {
      assert.equal(result.error.code, 'VALIDATION_ERROR');
      assert.match(result.error.message, /500/);
    }
  });

  it('returns not found when editing missing todo', async () => {
    const service = makeService();
    const result = await service.edit('missing-id', 'Updated text', 'todo:edit missing-id');

    assert.equal(result.ok, false);
    if (!result.ok) {
      assert.equal(result.error.code, 'NOT_FOUND');
      assert.deepEqual(result.error.meta, {
        action: 'edit',
        commitMessage: 'todo:edit missing-id',
        todoId: 'missing-id',
      });
    }
  });
});
