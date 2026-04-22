import { recordSyncFailure, recordSyncSuccess } from './syncMetadata.js';
import { GitHubAdapter } from './adapters/githubAdapter.js';

export class GitHubProvider {
  constructor(config) {
    this.adapter = new GitHubAdapter(config);
  }

  async listDirectory(path) {
    try {
      const response = await this.adapter.listDirectory(path, `listing maps directory ${path || '/'}`);
      recordSyncSuccess(`Listed ${path || '/'} from GitHub.`);
      return Array.isArray(response) ? response : [];
    } catch (error) {
      recordSyncFailure(toErrorSummary('list', path || '/', error));
      throw error;
    }
  }

  async getFile(path) {
    try {
      const response = await this.adapter.getContent(path, `loading remote file ${path}`);
      recordSyncSuccess(`Loaded ${path} from GitHub.`);
      return {
        content: await readTextContent(this.adapter, path, response, `loading remote file ${path}`),
        versionToken: response.sha,
      };
    } catch (error) {
      recordSyncFailure(toErrorSummary('read', path, error));
      throw error;
    }
  }

  async putFile(path, content, versionToken, message) {
    try {
      const response = await this.adapter.putContent(path, {
        message,
        content: encodeContent(content),
        sha: versionToken ?? undefined,
      }, `saving remote file ${path}`);
      recordSyncSuccess(`Saved ${path} to GitHub.`);
      return {
        versionToken: response.content?.sha ?? '',
        commitSha: response.commit?.sha,
      };
    } catch (error) {
      recordSyncFailure(toErrorSummary('write', path, error));
      throw error;
    }
  }
  async getFileRaw(path) {
    try {
      const response = await this.adapter.getContent(path, `loading raw file ${path}`);
      recordSyncSuccess(`Loaded ${path} from GitHub.`);
      return {
        base64Content: await readBase64Content(this.adapter, path, response, `loading raw file ${path}`),
        versionToken: response.sha,
      };
    } catch (error) {
      recordSyncFailure(toErrorSummary('read', path, error));
      throw error;
    }
  }

  async putBinaryFile(path, base64Content, versionToken, message) {
    try {
      const response = await this.adapter.putContent(path, {
        message,
        content: base64Content,
        sha: versionToken ?? undefined,
      }, `saving binary file ${path}`);
      recordSyncSuccess(`Saved ${path} to GitHub.`);
      return {
        versionToken: response.content?.sha ?? '',
        commitSha: response.commit?.sha,
      };
    } catch (error) {
      recordSyncFailure(toErrorSummary('write', path, error));
      throw error;
    }
  }

  async getFileAsBlob(path, mediaType = 'application/octet-stream') {
    try {
      const response = await this.adapter.getContent(path, `loading attachment ${path}`);
      recordSyncSuccess(`Loaded attachment ${path} from GitHub.`);
      return await readBlobContent(
        this.adapter,
        path,
        response,
        mediaType,
        `loading attachment ${path}`,
      );
    } catch (error) {
      recordSyncFailure(toErrorSummary('read', path, error));
      throw error;
    }
  }

  async deleteFile(path, versionToken, message) {
    try {
      const response = await this.adapter.deleteContent(path, {
        message,
        sha: versionToken,
      }, `deleting remote file ${path}`);
      recordSyncSuccess(`Deleted ${path} from GitHub.`);
      return {
        commitSha: response.commit?.sha,
      };
    } catch (error) {
      recordSyncFailure(toErrorSummary('delete', path, error));
      throw error;
    }
  }
}

function decodeContent(content, encoding) {
  if (encoding !== 'base64') {
    throw new Error(`Unsupported GitHub content encoding: ${encoding}`);
  }

  const sanitized = sanitizeBase64Content(content);
  const binary = atob(sanitized);
  const bytes = Uint8Array.from(binary, (character) => character.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

function decodeBlobContent(content, encoding, mediaType) {
  if (encoding !== 'base64') {
    throw new Error(`Unsupported GitHub content encoding: ${encoding}`);
  }

  const sanitized = sanitizeBase64Content(content);
  const binary = atob(sanitized);
  const bytes = Uint8Array.from(binary, (character) => character.charCodeAt(0));
  return new Blob([bytes], { type: mediaType });
}

async function readTextContent(adapter, path, response, contextLabel) {
  if (response?.encoding === 'base64') {
    return decodeContent(response.content, response.encoding);
  }

  if (response?.encoding === 'none') {
    return adapter.getContentText(path, contextLabel);
  }

  throw new Error(`Unsupported GitHub content encoding: ${response?.encoding}`);
}

async function readBase64Content(adapter, path, response, contextLabel) {
  if (response?.encoding === 'base64') {
    return sanitizeBase64Content(response.content);
  }

  if (response?.encoding === 'none') {
    const blob = await adapter.getContentBlob(path, contextLabel);
    return encodeBlobAsBase64(blob);
  }

  throw new Error(`Unsupported GitHub content encoding: ${response?.encoding}`);
}

async function readBlobContent(adapter, path, response, mediaType, contextLabel) {
  if (response?.encoding === 'base64') {
    return decodeBlobContent(response.content, response.encoding, mediaType);
  }

  if (response?.encoding === 'none') {
    const blob = await adapter.getContentBlob(path, contextLabel);
    return withBlobMediaType(blob, mediaType);
  }

  throw new Error(`Unsupported GitHub content encoding: ${response?.encoding}`);
}

function sanitizeBase64Content(content) {
  return String(content ?? '').replace(/\n/g, '');
}

async function encodeBlobAsBase64(blob) {
  const bytes = new Uint8Array(await blob.arrayBuffer());
  return encodeBytesAsBase64(bytes);
}

function encodeBytesAsBase64(bytes) {
  const bufferApi = globalThis.Buffer;
  if (bufferApi?.from) {
    return bufferApi.from(bytes).toString('base64');
  }

  let binary = '';
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });
  return btoa(binary);
}

async function withBlobMediaType(blob, mediaType) {
  if (!mediaType || blob.type === mediaType) {
    return blob;
  }

  return new Blob([await blob.arrayBuffer()], {
    type: mediaType,
  });
}

function encodeContent(content) {
  const bytes = new TextEncoder().encode(content);
  let binary = '';
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });
  return btoa(binary);
}

function toErrorSummary(operation, path, error) {
  const message = error instanceof Error ? error.message : String(error);
  return `Sync ${operation} failed for ${path}: ${message}`;
}
