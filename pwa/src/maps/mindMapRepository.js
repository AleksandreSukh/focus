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

  async createMap(filePath, document, commitMessage) {
    try {
      const outcome = await this.provider.saveMap({
        filePath,
        document,
        expectedRevision: null,
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
            code: 'ALREADY_EXISTS',
            message: `A map file named "${filePath}" already exists on the remote. Choose a different name.`,
            retriable: false,
          },
        };
      }

      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: outcome.message || `Unable to create map "${filePath}".`,
          retriable: true,
        },
      };
    } catch (cause) {
      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: cause?.message || `Unable to create map "${filePath}".`,
          retriable: cause?.code === 'NETWORK' || cause?.code === 'RATE_LIMIT',
          cause,
        },
      };
    }
  }

  async deleteMap(filePath, expectedRevision, commitMessage) {
    try {
      const outcome = await this.provider.deleteMap({
        filePath,
        expectedRevision,
        commitMessage,
      });

      if (outcome.ok) {
        return {
          ok: true,
        };
      }

      if (outcome.reason === 'conflict') {
        return {
          ok: false,
          error: {
            code: 'STALE_STATE',
            message: `Remote map "${filePath}" changed and must be refreshed before deleting.`,
            retriable: true,
          },
        };
      }

      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: outcome.message || `Unable to delete map "${filePath}".`,
          retriable: true,
        },
      };
    } catch (cause) {
      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: cause?.message || `Unable to delete map "${filePath}".`,
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
