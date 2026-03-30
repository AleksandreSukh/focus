import { mergeDocuments } from './merge';
import { TodoRepository } from './todoRepository';
import {
  Todo,
  TodoError,
  TodoMutationMeta,
  TodoResult,
  TodosDocument,
  TodosSnapshot,
} from './types';

const CURRENT_VERSION = 1;
const MAX_TEXT_LENGTH = 500;

const nowIso = (): string => new Date().toISOString();

const createTodo = (text: string): Todo => {
  const timestamp = nowIso();
  return {
    id: globalThis.crypto?.randomUUID?.() ?? `${timestamp}-${Math.random().toString(16).slice(2)}`,
    text,
    completed: false,
    deleted: false,
    createdAt: timestamp,
    updatedAt: timestamp,
  };
};

const normalize = (doc: TodosDocument): TodosDocument => ({
  version: doc.version ?? CURRENT_VERSION,
  items: [...doc.items],
});

export class TodoService {
  private state: TodosSnapshot | null = null;

  constructor(private readonly repository: TodoRepository) {}

  async list(forceRefresh = false): Promise<TodoResult<Todo[]>> {
    if (!this.state || forceRefresh) {
      const loaded = await this.repository.loadLatest();
      if (!loaded.ok) {
        return loaded;
      }
      this.state = loaded.value;
    }

    return {
      ok: true,
      value: this.state.document.items.filter((item) => !item.deleted),
      revision: this.state.revision,
      mergedAfterConflict: false,
    };
  }

  async add(text: string, commitMessage: string): Promise<TodoResult<Todo>> {
    return this.mutate(
      { action: 'add', commitMessage },
      (document) => {
        const normalized = text.trim();
        const validationError = this.validateText(normalized, { action: 'add', commitMessage });
        if (validationError) {
          return validationError;
        }

        const todo = createTodo(normalized);
        document.items = [...document.items, todo];
        return todo;
      },
    );
  }

  async edit(todoId: string, text: string, commitMessage: string): Promise<TodoResult<Todo>> {
    return this.mutate(
      { action: 'edit', commitMessage, todoId },
      (document) => {
        const normalized = text.trim();
        const validationError = this.validateText(normalized, { action: 'edit', commitMessage, todoId });
        if (validationError) {
          return validationError;
        }

        const todo = document.items.find((item) => item.id === todoId && !item.deleted);
        if (!todo) {
          return this.notFound(todoId, { action: 'edit', commitMessage, todoId });
        }

        todo.text = normalized;
        todo.updatedAt = nowIso();
        return { ...todo };
      },
    );
  }

  async toggle(todoId: string, commitMessage: string): Promise<TodoResult<Todo>> {
    return this.mutate(
      { action: 'toggle', commitMessage, todoId },
      (document) => {
        const todo = document.items.find((item) => item.id === todoId && !item.deleted);
        if (!todo) {
          return this.notFound(todoId, { action: 'toggle', commitMessage, todoId });
        }

        todo.completed = !todo.completed;
        todo.updatedAt = nowIso();
        return { ...todo };
      },
    );
  }

  async delete(todoId: string, commitMessage: string): Promise<TodoResult<Todo>> {
    return this.mutate(
      { action: 'delete', commitMessage, todoId },
      (document) => {
        const todo = document.items.find((item) => item.id === todoId && !item.deleted);
        if (!todo) {
          return this.notFound(todoId, { action: 'delete', commitMessage, todoId });
        }

        todo.deleted = true;
        todo.updatedAt = nowIso();
        return { ...todo };
      },
    );
  }

  private async ensureLatestState(): Promise<TodoResult<TodosSnapshot>> {
    const loaded = await this.repository.loadLatest();
    if (!loaded.ok) {
      return loaded;
    }

    if (!this.state || this.repository.isStale(this.state.revision, loaded.value.revision)) {
      this.state = loaded.value;
    }

    return {
      ok: true,
      value: this.state,
      revision: this.state.revision,
      mergedAfterConflict: false,
    };
  }

  private async mutate<T>(
    meta: TodoMutationMeta,
    apply: (document: TodosDocument) => T | TodoError,
  ): Promise<TodoResult<T>> {
    const latest = await this.ensureLatestState();
    if (!latest.ok) {
      return latest;
    }

    const baseRevision = latest.value.revision;
    const attempted = normalize(structuredClone(latest.value.document));
    const applied = apply(attempted);

    if (this.isError(applied)) {
      return { ok: false, error: applied };
    }

    const firstSave = await this.repository.save(attempted, baseRevision, meta.commitMessage);
    if (firstSave.ok) {
      this.state = {
        document: attempted,
        revision: firstSave.revision,
        loadedAt: Date.now(),
      };

      return {
        ok: true,
        value: applied,
        revision: firstSave.revision,
        mergedAfterConflict: false,
      };
    }

    if (firstSave.error.code !== 'STALE_STATE') {
      return {
        ok: false,
        error: { ...firstSave.error, meta },
      };
    }

    const reloaded = await this.repository.loadLatest();
    if (!reloaded.ok) {
      return {
        ok: false,
        error: { ...reloaded.error, meta },
      };
    }

    const merged = mergeDocuments(reloaded.value.document, attempted);
    const retry = await this.repository.save(merged, reloaded.value.revision, meta.commitMessage);

    if (!retry.ok) {
      return {
        ok: false,
        error: {
          code: 'CONFLICT_UNRESOLVED',
          message: 'Todo update conflicted twice and needs manual resolution.',
          retriable: false,
          meta,
          conflict: {
            latest: reloaded.value.document,
            attempted,
            merged,
          },
        },
      };
    }

    this.state = {
      document: merged,
      revision: retry.revision,
      loadedAt: Date.now(),
    };

    const refreshed = this.findResult(merged, meta.todoId, meta.action, applied);
    return {
      ok: true,
      value: refreshed,
      revision: retry.revision,
      mergedAfterConflict: true,
    };
  }

  private findResult<T>(
    merged: TodosDocument,
    todoId: string | undefined,
    action: TodoMutationMeta['action'],
    fallback: T,
  ): T {
    if (!todoId || action === 'add') {
      return fallback;
    }

    const todo = merged.items.find((item) => item.id === todoId);
    return (todo ? ({ ...todo } as T) : fallback);
  }

  private validateText(text: string, meta: TodoMutationMeta): TodoError | null {
    if (!text) {
      return {
        code: 'VALIDATION_ERROR',
        message: 'Todo text cannot be empty.',
        retriable: false,
        meta,
      };
    }

    if (text.length > MAX_TEXT_LENGTH) {
      return {
        code: 'VALIDATION_ERROR',
        message: `Todo text cannot exceed ${MAX_TEXT_LENGTH} characters.`,
        retriable: false,
        meta,
      };
    }

    return null;
  }

  private notFound(todoId: string, meta: TodoMutationMeta): TodoError {
    return {
      code: 'NOT_FOUND',
      message: `Todo "${todoId}" was not found.`,
      retriable: false,
      meta,
    };
  }

  private isError<T>(value: T | TodoError): value is TodoError {
    return typeof value === 'object' && value !== null && 'code' in value;
  }
}
