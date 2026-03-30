import {
  SaveOutcome,
  TodoError,
  TodoProvider,
  TodoResult,
  TodosDocument,
  TodosSnapshot,
} from './types';

export class TodoRepository {
  constructor(private readonly provider: TodoProvider) {}

  async loadLatest(): Promise<TodoResult<TodosSnapshot>> {
    try {
      const snapshot = await this.provider.load();
      return {
        ok: true,
        value: snapshot,
        revision: snapshot.revision,
        mergedAfterConflict: false,
      };
    } catch (cause) {
      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: 'Unable to load todos from provider.',
          retriable: true,
          cause,
        },
      };
    }
  }

  async save(
    document: TodosDocument,
    expectedRevision: string,
    commitMessage: string,
  ): Promise<TodoResult<string>> {
    let outcome: SaveOutcome;

    try {
      outcome = await this.provider.save({
        document,
        expectedRevision,
        commitMessage,
      });
    } catch (cause) {
      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: 'Unexpected provider failure while saving todos.',
          retriable: true,
          cause,
        },
      };
    }

    if (outcome.ok) {
      return {
        ok: true,
        value: outcome.revision,
        revision: outcome.revision,
        mergedAfterConflict: false,
      };
    }

    if (outcome.reason === 'conflict') {
      return {
        ok: false,
        error: {
          code: 'STALE_STATE',
          message: 'Remote todo state changed and must be refreshed.',
          retriable: true,
        },
      };
    }

    return {
      ok: false,
      error: {
        code: 'PERSISTENCE_ERROR',
        message: outcome.message,
        retriable: true,
      },
    };
  }

  isStale(currentRevision: string | undefined, latestRevision: string): boolean {
    return !currentRevision || currentRevision !== latestRevision;
  }

  staleError(message: string): TodoError {
    return {
      code: 'STALE_STATE',
      message,
      retriable: true,
    };
  }
}
