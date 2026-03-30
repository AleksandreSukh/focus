import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { TodoRepository } from './todoRepository.ts';
import { TodoService } from './todoService.ts';
import { SaveRequest, SaveOutcome, TodoProvider, TodosDocument, TodosSnapshot } from './types.ts';

class ScriptedProvider implements TodoProvider {
  public loads = 0;
  public saves: SaveRequest[] = [];

  private snapshot: TodosSnapshot;
  private readonly scriptedOutcomes: SaveOutcome[];

  constructor(snapshot: TodosSnapshot, scriptedOutcomes: SaveOutcome[] = []) {
    this.snapshot = structuredClone(snapshot);
    this.scriptedOutcomes = [...scriptedOutcomes];
  }

  async load(): Promise<TodosSnapshot> {
    this.loads += 1;
    return structuredClone(this.snapshot);
  }

  async save(request: SaveRequest): Promise<SaveOutcome> {
    this.saves.push(structuredClone(request));

    const scripted = this.scriptedOutcomes.shift();
    if (scripted) {
      if (scripted.ok) {
        this.snapshot = {
          document: structuredClone(request.document),
          revision: scripted.revision,
          loadedAt: Date.now(),
        };
      }
      return scripted;
    }

    if (request.expectedRevision !== this.snapshot.revision) {
      return { ok: false, reason: 'conflict', revision: this.snapshot.revision };
    }

    const revision = `rev-${Date.now()}-${this.saves.length}`;
    this.snapshot = {
      document: structuredClone(request.document),
      revision,
      loadedAt: Date.now(),
    };

    return { ok: true, revision };
  }

  setRemoteDocument(document: TodosDocument, revision: string): void {
    this.snapshot = {
      document: structuredClone(document),
      revision,
      loadedAt: Date.now(),
    };
  }
}

describe('TodoService integration with mock provider', () => {
  it('loads latest todos and saves an add mutation', async () => {
    const provider = new ScriptedProvider({
      document: { version: 1, items: [] },
      revision: 'rev-1',
      loadedAt: Date.now(),
    });
    const service = new TodoService(new TodoRepository(provider));

    const list = await service.list();
    assert.equal(list.ok, true);
    if (list.ok) {
      assert.deepEqual(list.value, []);
    }

    const added = await service.add('Write integration test', 'todo:add Write integration test');

    assert.equal(added.ok, true);
    assert.equal(provider.loads >= 1, true);
    assert.equal(provider.saves.length, 1);
    assert.equal(provider.saves[0].commitMessage, 'todo:add Write integration test');
  });

  it('retries save after conflict by loading latest and merging', async () => {
    const initialRemote: TodosSnapshot = {
      document: {
        version: 1,
        items: [
          {
            id: 'todo-1',
            text: 'Remote title',
            completed: false,
            deleted: false,
            createdAt: '2026-01-01T00:00:00.000Z',
            updatedAt: '2026-01-01T00:00:00.000Z',
          },
        ],
      },
      revision: 'rev-1',
      loadedAt: Date.now(),
    };

    const provider = new ScriptedProvider(initialRemote, [
      { ok: false, reason: 'conflict', revision: 'rev-2' },
      { ok: true, revision: 'rev-3' },
    ]);

    const service = new TodoService(new TodoRepository(provider));

    await service.list();

    provider.setRemoteDocument(
      {
        version: 1,
        items: [
          {
            id: 'todo-1',
            text: 'Remote title updated elsewhere',
            completed: false,
            deleted: false,
            createdAt: '2026-01-01T00:00:00.000Z',
            updatedAt: '2026-01-01T00:00:02.000Z',
          },
        ],
      },
      'rev-2',
    );

    const edited = await service.edit('todo-1', 'Local update that conflicts', 'todo:edit todo-1');

    assert.equal(edited.ok, true);
    if (edited.ok) {
      assert.equal(edited.mergedAfterConflict, true);
      assert.equal(edited.revision, 'rev-3');
      assert.equal(edited.value.text, 'Remote title updated elsewhere');
    }

    assert.equal(provider.saves.length, 2);
    assert.equal(provider.saves[0].expectedRevision, 'rev-1');
    assert.equal(provider.saves[1].expectedRevision, 'rev-2');
  });
});
