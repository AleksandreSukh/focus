import { GitHubApiError } from '../gitProvider/adapters/githubAdapter.js';
import { GitHubProvider } from '../gitProvider/githubProvider.js';
import {
  normalizeMindMapDocument,
  parseMindMapDocument,
  serializeMindMapDocument,
} from './model.js';

export class GitHubMindMapProvider {
  constructor({ owner, repo, branch, token, directoryPath }) {
    this.directoryPath = String(directoryPath ?? '').replace(/^\/+|\/+$/g, '');
    this.gitProvider = new GitHubProvider({
      owner,
      repo,
      branch,
      token,
    });
  }

  async listMapFiles() {
    const entries = await this.gitProvider.listDirectory(this.directoryPath);
    return entries
      .filter((entry) =>
        entry &&
        entry.type === 'file' &&
        typeof entry.name === 'string' &&
        entry.name.toLowerCase().endsWith('.json'),
      )
      .map((entry) => ({
        fileName: entry.name,
        filePath: entry.path,
      }))
      .sort((left, right) => left.fileName.localeCompare(right.fileName));
  }

  async loadMap(filePath) {
    const snapshot = await this.gitProvider.getFile(filePath);
    const fileName = filePath.split('/').pop() || filePath;
    const mapName = fileName.replace(/\.json$/i, '');
    return {
      filePath,
      fileName,
      mapName,
      document: normalizeMindMapDocument(parseMindMapDocument(snapshot.content)),
      revision: snapshot.versionToken,
      loadedAt: Date.now(),
    };
  }

  async getAttachmentBlob(mapFilePath, relativePath, mediaType) {
    const attachmentDir = mapFilePath.replace(/\.json$/i, '_attachments');
    const attachmentPath = `${attachmentDir}/${relativePath}`;
    return this.gitProvider.getFileAsBlob(attachmentPath, mediaType || 'image/png');
  }

  async deleteMap({ filePath, expectedRevision, commitMessage }) {
    try {
      const result = await this.gitProvider.deleteFile(
        filePath,
        expectedRevision,
        commitMessage,
      );

      return {
        ok: true,
        commitSha: result.commitSha,
      };
    } catch (error) {
      if (error instanceof GitHubApiError && error.code === 'CONFLICT') {
        return {
          ok: false,
          reason: 'conflict',
        };
      }

      throw error;
    }
  }

  async saveMap({ filePath, document, expectedRevision, commitMessage }) {
    try {
      const result = await this.gitProvider.putFile(
        filePath,
        serializeMindMapDocument(normalizeMindMapDocument(document)),
        expectedRevision,
        commitMessage,
      );

      return {
        ok: true,
        revision: result.versionToken,
      };
    } catch (error) {
      if (error instanceof GitHubApiError && error.code === 'CONFLICT') {
        return {
          ok: false,
          reason: 'conflict',
        };
      }

      throw error;
    }
  }
}
