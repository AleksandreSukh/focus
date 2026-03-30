export class MindMapRepository {
  constructor(provider) {
    this.provider = provider;
  }

  async listFiles() {
    try {
      return {
        ok: true,
        value: await this.provider.listMapFiles(),
      };
    } catch (cause) {
      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: cause?.message || 'Unable to list map files from provider.',
          retriable: cause?.code === 'NETWORK' || cause?.code === 'RATE_LIMIT',
          cause,
        },
      };
    }
  }

  async loadMap(filePath) {
    try {
      return {
        ok: true,
        value: await this.provider.loadMap(filePath),
      };
    } catch (cause) {
      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: cause?.message || `Unable to load map "${filePath}".`,
          retriable: cause?.code === 'NETWORK' || cause?.code === 'RATE_LIMIT',
          cause,
        },
      };
    }
  }

  async saveMap(filePath, document, expectedRevision, commitMessage) {
    try {
      const outcome = await this.provider.saveMap({
        filePath,
        document,
        expectedRevision,
        commitMessage,
      });

      if (outcome.ok) {
        return {
          ok: true,
          revision: outcome.revision,
        };
      }

      if (outcome.reason === 'conflict') {
        return {
          ok: false,
          error: {
            code: 'STALE_STATE',
            message: `Remote map "${filePath}" changed and must be refreshed.`,
            retriable: true,
          },
        };
      }

      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: outcome.message || `Unable to save map "${filePath}".`,
          retriable: true,
        },
      };
    } catch (cause) {
      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: cause?.message || `Unable to save map "${filePath}".`,
          retriable: cause?.code === 'NETWORK' || cause?.code === 'RATE_LIMIT',
          cause,
        },
      };
    }
  }
}
