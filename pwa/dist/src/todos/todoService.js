import { mergeDocuments } from './merge.js';

const CURRENT_VERSION = 1;
const MAX_TEXT_LENGTH = 500;

function nowIso() {
  return new Date().toISOString();
}

function clone(value) {
  if (typeof structuredClone === 'function') {
    return structuredClone(value);
  }

  return JSON.parse(JSON.stringify(value));
}

function createTodo(text, todoId) {
  const timestamp = nowIso();
  return {
    id: todoId || globalThis.crypto?.randomUUID?.() || `${timestamp}-${Math.random().toString(16).slice(2)}`,
    text,
    completed: false,
    deleted: false,
    createdAt: timestamp,
    updatedAt: timestamp,
  };
}

function normalizeDocument(document) {
  return {
    version: document.version ?? CURRENT_VERSION,
    items: [...document.items],
  };
}

export class TodoService {
  constructor(repository) {
    this.repository = repository;
    this.state = null;
  }

  async list(forceRefresh = false) {
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

  async add(text, commitMessage, todoId) {
    return this.mutate(
      { action: 'add', commitMessage, todoId },
      (document) => {
        const normalized = String(text ?? '').trim();
        const validationError = this.validateText(normalized, { action: 'add', commitMessage, todoId });
        if (validationError) {
          return validationError;
        }

        const todo = createTodo(normalized, todoId);
        document.items = [...document.items, todo];
        return todo;
      },
    );
  }

  async edit(todoId, text, commitMessage) {
    return this.mutate(
      { action: 'edit', commitMessage, todoId },
      (document) => {
        const normalized = String(text ?? '').trim();
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

  async setCompleted(todoId, completed, commitMessage) {
    return this.mutate(
      { action: 'toggle', commitMessage, todoId },
      (document) => {
        const todo = document.items.find((item) => item.id === todoId && !item.deleted);
        if (!todo) {
          return this.notFound(todoId, { action: 'toggle', commitMessage, todoId });
        }

        todo.completed = Boolean(completed);
        todo.updatedAt = nowIso();
        return { ...todo };
      },
    );
  }

  async toggle(todoId, commitMessage) {
    const existing = await this.list();
    if (!existing.ok) {
      return existing;
    }

    const todo = existing.value.find((item) => item.id === todoId);
    if (!todo) {
      return {
        ok: false,
        error: this.notFound(todoId, { action: 'toggle', commitMessage, todoId }),
      };
    }

    return this.setCompleted(todoId, !todo.completed, commitMessage);
  }

  async delete(todoId, commitMessage) {
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

  async ensureLatestState() {
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

  async mutate(meta, apply) {
    const latest = await this.ensureLatestState();
    if (!latest.ok) {
      return latest;
    }

    const baseRevision = latest.value.revision;
    const attempted = normalizeDocument(clone(latest.value.document));
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

  findResult(merged, todoId, action, fallback) {
    if (!todoId || action === 'add') {
      return fallback;
    }

    const todo = merged.items.find((item) => item.id === todoId);
    return todo ? { ...todo } : fallback;
  }

  validateText(text, meta) {
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

  notFound(todoId, meta) {
    return {
      code: 'NOT_FOUND',
      message: `Todo "${todoId}" was not found.`,
      retriable: false,
      meta,
    };
  }

  isError(value) {
    return typeof value === 'object' && value !== null && 'code' in value;
  }
}
