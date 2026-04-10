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
      if (cause?.code === 'UNREADABLE_MAP') {
        return {
          ok: false,
          error: {
            code: 'UNREADABLE_MAP',
            reason: typeof cause.reason === 'string' ? cause.reason : 'unknown',
            message: cause?.message || `Map "${filePath}" cannot be loaded.`,
            retriable: true,
            filePath: cause.filePath || filePath,
            fileName: cause.fileName || (filePath.split('/').pop() || filePath),
            mapName: cause.mapName || (filePath.split('/').pop() || filePath).replace(/\.json$/i, ''),
            revision: typeof cause.revision === 'string' ? cause.revision : '',
            rawText: typeof cause.rawText === 'string' ? cause.rawText : '',
            cause: cause.cause ?? cause,
          },
        };
      }

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

  async loadAttachment(mapFilePath, relativePath, mediaType) {
    try {
      const blob = await this.provider.getAttachmentBlob(mapFilePath, relativePath, mediaType);
      return {
        ok: true,
        value: blob,
      };
    } catch (cause) {
      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: cause?.message || `Unable to load attachment "${relativePath}".`,
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

  async renameMap(oldFilePath, newFilePath, document, oldRevision, commitMessage) {
    try {
      const createOutcome = await this.provider.saveMap({
        filePath: newFilePath,
        document,
        expectedRevision: null,
        commitMessage,
      });

      if (!createOutcome.ok) {
        if (createOutcome.reason === 'conflict') {
          return {
            ok: false,
            error: {
              code: 'ALREADY_EXISTS',
              message: `A map file named "${newFilePath}" already exists on the remote. Choose a different name.`,
              retriable: false,
            },
          };
        }

        return {
          ok: false,
          error: {
            code: 'PERSISTENCE_ERROR',
            message: createOutcome.message || `Unable to create map "${newFilePath}".`,
            retriable: true,
          },
        };
      }

      const deleteOutcome = await this.provider.deleteMap({
        filePath: oldFilePath,
        expectedRevision: oldRevision,
        commitMessage,
      });

      if (!deleteOutcome.ok) {
        if (deleteOutcome.reason === 'conflict') {
          return {
            ok: false,
            error: {
              code: 'STALE_STATE',
              message: `Remote map "${oldFilePath}" changed during rename. Refresh and try again.`,
              retriable: true,
            },
          };
        }

        return {
          ok: false,
          error: {
            code: 'PERSISTENCE_ERROR',
            message: deleteOutcome.message || `Unable to delete old map "${oldFilePath}" after rename.`,
            retriable: true,
          },
        };
      }

      return {
        ok: true,
        revision: createOutcome.revision,
      };
    } catch (cause) {
      return {
        ok: false,
        error: {
          code: 'PERSISTENCE_ERROR',
          message: cause?.message || `Unable to rename map "${oldFilePath}".`,
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
