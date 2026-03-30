export class TodoRepository {
  constructor(provider) {
    this.provider = provider;
  }

  async loadLatest() {
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
          message: cause?.message || 'Unable to load todos from provider.',
          retriable: cause?.code === 'NETWORK' || cause?.code === 'RATE_LIMIT',
          cause,
        },
      };
    }
  }

  async save(document, expectedRevision, commitMessage) {
    let outcome;

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
          message: cause?.message || 'Unexpected provider failure while saving todos.',
          retriable: cause?.code === 'NETWORK' || cause?.code === 'RATE_LIMIT',
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

  isStale(currentRevision, latestRevision) {
    return !currentRevision || currentRevision !== latestRevision;
  }
}
