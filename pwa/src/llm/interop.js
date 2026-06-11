import {
  NODE_TYPE,
  TASK_STATE,
  findNodeRecord,
  normalizeNodeDisplayText,
  nowIso,
} from '../maps/model.js';
import {
  collectBacklinkRelatedNodeEntries,
  collectOutgoingRelatedNodeEntries,
} from '../maps/relatedNodes.js';

export const LLM_PROMPT_PREFIX = '@ai ';
export const LLM_CONTEXT_MODE = 'subtree-links';
export const LLM_JOB_VERSION = 1;

export const LLM_JOB_STATUS = Object.freeze({
  PENDING: 'pending',
  CLAIMED: 'claimed',
  COMPLETED: 'completed',
  FAILED: 'failed',
});

const URL_PATTERN = /\bhttps?:\/\/[^\s<>"')\]]+/gi;
const DEFAULT_AGENT = 'focus-interop';

export function isLlmPromptNode(node) {
  return Boolean(
    node &&
    node.nodeType !== NODE_TYPE.IDEA_BAG_ITEM &&
    (node.taskState === TASK_STATE.TODO || node.taskState === TASK_STATE.DOING) &&
    extractLlmPromptText(node.name),
  );
}

export function extractLlmPromptText(value) {
  const text = typeof value === 'string' ? value.trim() : '';
  if (!text.toLowerCase().startsWith(LLM_PROMPT_PREFIX)) {
    return '';
  }

  return text.slice(LLM_PROMPT_PREFIX.length).trim();
}

export function createLlmJob({
  jobId = createGuid(),
  mapFilePath,
  nodeId,
  prompt,
  createdAt = nowIso(),
  mode = LLM_CONTEXT_MODE,
} = {}) {
  const timestamp = normalizeTimestamp(createdAt);
  return normalizeLlmJob({
    version: LLM_JOB_VERSION,
    id: jobId,
    status: LLM_JOB_STATUS.PENDING,
    mode,
    mapFilePath,
    nodeId,
    prompt,
    createdAt: timestamp,
    updatedAt: timestamp,
    claimedBy: null,
    claimedAt: null,
    completedAt: null,
    failedAt: null,
    errorMessage: null,
    result: null,
  });
}

export function normalizeLlmJob(rawJob = {}, options = {}) {
  const createdAt = normalizeTimestamp(rawJob.createdAt || options.createdAt || nowIso());
  const status = Object.values(LLM_JOB_STATUS).includes(rawJob.status)
    ? rawJob.status
    : LLM_JOB_STATUS.PENDING;
  const id = normalizeText(rawJob.id || options.jobId || createGuid());
  const mapFilePath = normalizeText(rawJob.mapFilePath || rawJob.mapPath);
  const nodeId = normalizeText(rawJob.nodeId);
  const prompt = normalizeText(rawJob.prompt);
  const updatedAt = normalizeTimestamp(rawJob.updatedAt || createdAt);

  return {
    ...rawJob,
    version: Number.isInteger(rawJob.version) ? rawJob.version : LLM_JOB_VERSION,
    id,
    status,
    mode: rawJob.mode === LLM_CONTEXT_MODE ? rawJob.mode : LLM_CONTEXT_MODE,
    mapFilePath,
    nodeId,
    prompt,
    createdAt,
    updatedAt,
    claimedBy: normalizeNullableText(rawJob.claimedBy),
    claimedAt: normalizeNullableTimestamp(rawJob.claimedAt),
    completedAt: normalizeNullableTimestamp(rawJob.completedAt),
    failedAt: normalizeNullableTimestamp(rawJob.failedAt),
    errorMessage: normalizeNullableText(rawJob.errorMessage),
    result: isPlainObject(rawJob.result) ? rawJob.result : null,
  };
}

export function serializeLlmJob(job) {
  return `${JSON.stringify(normalizeLlmJob(job), null, 2)}\n`;
}

export function collectLlmPromptEntries(snapshots, jobs = []) {
  const referencedNodes = new Set(
    jobs
      .map((job) => buildNodeReferenceKey(job.mapFilePath, job.nodeId))
      .filter(Boolean),
  );
  const entries = [];

  normalizeSnapshots(snapshots).forEach((snapshot) => {
    traverseNode(snapshot.document?.rootNode, null, [], 0, (node, parent, context) => {
      if (!parent || !isLlmPromptNode(node)) {
        return;
      }

      const referenceKey = buildNodeReferenceKey(snapshot.filePath, node.uniqueIdentifier);
      if (referencedNodes.has(referenceKey)) {
        return;
      }

      entries.push({
        mapFilePath: snapshot.filePath,
        mapName: snapshot.mapName,
        nodeId: node.uniqueIdentifier,
        prompt: extractLlmPromptText(node.name),
        nodePath: context.pathSegments.join(' > '),
        nodePathSegments: [...context.pathSegments],
      });
    });
  });

  return entries.sort((left, right) =>
    left.mapFilePath.localeCompare(right.mapFilePath) ||
    left.nodePath.localeCompare(right.nodePath));
}

export function buildLlmContext({ snapshot, nodeId, snapshots = [] } = {}) {
  const document = snapshot?.document;
  const record = findNodeRecord(document, nodeId);
  if (!snapshot || !record) {
    return null;
  }

  const allSnapshots = normalizeSnapshots(snapshots).length > 0
    ? normalizeSnapshots(snapshots)
    : [snapshot];
  const pathRecords = findNodePath(document.rootNode, nodeId);
  const promptText = extractLlmPromptText(record.node.name) || normalizeNodeDisplayText(record.node.name);
  const subtree = buildContextNode(record.node);
  const outgoing = collectOutgoingRelatedNodeEntries(record.node, allSnapshots);
  const backlinks = collectBacklinkRelatedNodeEntries(record.node.uniqueIdentifier, allSnapshots);
  const urls = collectContextUrls({
    ancestors: pathRecords.map((item) => item.node),
    subtree: record.node,
    outgoing,
    backlinks,
  });

  return {
    version: 1,
    mode: LLM_CONTEXT_MODE,
    generatedAt: nowIso(),
    map: {
      filePath: snapshot.filePath,
      fileName: snapshot.fileName,
      mapName: snapshot.mapName,
      updatedAt: snapshot.document?.updatedAt || '',
    },
    prompt: {
      nodeId: record.node.uniqueIdentifier,
      text: promptText,
      rawText: record.node.name || '',
      path: record.pathSegments.join(' > '),
      pathSegments: [...record.pathSegments],
      taskState: taskStateLabel(record.node.taskState),
    },
    ancestors: pathRecords.slice(0, -1).map((item) => ({
      nodeId: item.node.uniqueIdentifier,
      name: normalizeNodeDisplayText(item.node.name),
      nodeType: item.node.nodeType,
      taskState: taskStateLabel(item.node.taskState),
      depth: item.depth,
    })),
    subtree,
    links: {
      outgoing: outgoing.map(normalizeRelatedEntry),
      backlinks: backlinks.map(normalizeRelatedEntry),
    },
    urls,
  };
}

export function formatLlmContextMarkdown(context) {
  if (!context) {
    return '';
  }

  const lines = [
    `# ${context.prompt.text}`,
    '',
    `Map: ${context.map.mapName} (${context.map.filePath})`,
    `Node: ${context.prompt.nodeId}`,
    `Path: ${context.prompt.path}`,
    '',
    '## Tree',
  ];

  appendMarkdownNode(lines, context.subtree, 0);

  if (context.links.outgoing.length > 0) {
    lines.push('', '## Outgoing Links');
    context.links.outgoing.forEach((entry) => {
      lines.push(`- ${entry.relationLabel}: ${entry.mapName} > ${entry.nodePath} (${entry.nodeId})`);
    });
  }

  if (context.links.backlinks.length > 0) {
    lines.push('', '## Backlinks');
    context.links.backlinks.forEach((entry) => {
      lines.push(`- ${entry.relationLabel}: ${entry.mapName} > ${entry.nodePath} (${entry.nodeId})`);
    });
  }

  if (context.urls.length > 0) {
    lines.push('', '## Links Found In Text');
    context.urls.forEach((url) => {
      lines.push(`- ${url.url}`);
    });
  }

  return `${lines.join('\n')}\n`;
}

export function claimLlmJob(job, { agent = DEFAULT_AGENT, timestamp = nowIso() } = {}) {
  const normalized = normalizeLlmJob(job);
  const claimedAt = normalizeTimestamp(timestamp);
  return normalizeLlmJob({
    ...normalized,
    status: LLM_JOB_STATUS.CLAIMED,
    claimedBy: normalizeText(agent) || DEFAULT_AGENT,
    claimedAt,
    updatedAt: claimedAt,
  });
}

export function completeLlmJob(job, { agent = DEFAULT_AGENT, timestamp = nowIso(), result = {} } = {}) {
  const normalized = normalizeLlmJob(job);
  const completedAt = normalizeTimestamp(timestamp);
  return normalizeLlmJob({
    ...normalized,
    status: LLM_JOB_STATUS.COMPLETED,
    completedAt,
    updatedAt: completedAt,
    errorMessage: null,
    result: {
      ...result,
      completedBy: normalizeText(agent) || normalized.claimedBy || DEFAULT_AGENT,
      completedAt,
    },
  });
}

export function failLlmJob(job, { message, timestamp = nowIso() } = {}) {
  const normalized = normalizeLlmJob(job);
  const failedAt = normalizeTimestamp(timestamp);
  return normalizeLlmJob({
    ...normalized,
    status: LLM_JOB_STATUS.FAILED,
    failedAt,
    updatedAt: failedAt,
    errorMessage: normalizeText(message) || 'The agent did not provide a failure message.',
  });
}

export function applyLlmJobCompletion(document, job, { answer, agent = DEFAULT_AGENT, timestamp = nowIso(), answerNodeId = createGuid() } = {}) {
  const normalized = normalizeLlmJob(job);
  const text = String(answer ?? '').trim();
  if (!text) {
    return {
      ok: false,
      error: {
        code: 'VALIDATION_ERROR',
        message: 'LLM answer cannot be empty.',
      },
    };
  }

  const record = findNodeRecord(document, normalized.nodeId);
  if (!record) {
    return {
      ok: false,
      error: {
        code: 'NOT_FOUND',
        message: `Prompt node "${normalized.nodeId}" was not found.`,
      },
    };
  }

  const completedAt = normalizeTimestamp(timestamp);
  const agentName = normalizeText(agent) || normalized.claimedBy || DEFAULT_AGENT;
  const answerNode = createAnswerNode({
    nodeId: answerNodeId,
    answer: text,
    agent: agentName,
    timestamp: completedAt,
    number: record.node.children.length + 1,
  });

  record.node.children.push(answerNode);
  renumberChildren(record.node);
  record.node.taskState = TASK_STATE.DONE;
  touchNode(record.node, completedAt);
  document.updatedAt = completedAt;

  return {
    ok: true,
    value: {
      answerNodeId: answerNode.uniqueIdentifier,
      promptNodeId: record.node.uniqueIdentifier,
      completedAt,
      agent: agentName,
    },
  };
}

function buildContextNode(node) {
  return {
    nodeId: node.uniqueIdentifier,
    name: normalizeNodeDisplayText(node.name),
    rawText: typeof node.name === 'string' ? node.name : '',
    nodeType: node.nodeType,
    taskState: taskStateLabel(node.taskState),
    links: normalizeLinks(node.links),
    urls: extractUrls(node.name),
    children: Array.isArray(node.children)
      ? node.children.map((child) => buildContextNode(child))
      : [],
  };
}

function appendMarkdownNode(lines, node, depth) {
  if (!node) {
    return;
  }

  const indent = '  '.repeat(depth);
  const marker = node.taskState === 'none' ? '-' : `- [${node.taskState}]`;
  lines.push(`${indent}${marker} ${node.name} (${node.nodeId})`);
  if (node.rawText && node.rawText.includes('\n')) {
    node.rawText.split(/\r\n|\r|\n/).slice(1).forEach((line) => {
      lines.push(`${indent}  > ${line}`);
    });
  }

  node.children.forEach((child) => appendMarkdownNode(lines, child, depth + 1));
}

function collectContextUrls({ ancestors, subtree, outgoing, backlinks }) {
  const entries = [];
  const seen = new Set();
  const addUrl = (url, source) => {
    if (seen.has(url)) {
      return;
    }
    seen.add(url);
    entries.push({ url, source });
  };

  ancestors.forEach((node) => {
    extractUrls(node.name).forEach((url) => addUrl(url, `ancestor:${node.uniqueIdentifier}`));
  });

  traverseNode(subtree, null, [], 0, (node) => {
    extractUrls(node.name).forEach((url) => addUrl(url, `subtree:${node.uniqueIdentifier}`));
  });

  [...outgoing, ...backlinks].forEach((entry) => {
    extractUrls(entry.nodeName).forEach((url) => addUrl(url, `link:${entry.nodeId}`));
  });

  return entries.sort((left, right) => left.url.localeCompare(right.url) || left.source.localeCompare(right.source));
}

function normalizeRelatedEntry(entry) {
  return {
    direction: entry.direction,
    relationLabel: entry.relationLabel,
    mapPath: entry.mapPath,
    mapName: entry.mapName,
    nodeId: entry.nodeId,
    nodeName: entry.nodeName,
    nodePath: entry.nodePath,
    nodePathSegments: Array.isArray(entry.nodePathSegments) ? [...entry.nodePathSegments] : [],
  };
}

function normalizeLinks(links) {
  if (!isPlainObject(links)) {
    return [];
  }

  return Object.values(links)
    .filter((link) => link && typeof link.id === 'string')
    .map((link) => ({
      nodeId: link.id,
      relationType: Number.isInteger(link.relationType) ? link.relationType : 0,
      metadata: typeof link.metadata === 'string' ? link.metadata : null,
    }))
    .sort((left, right) => left.nodeId.localeCompare(right.nodeId));
}

function findNodePath(rootNode, nodeId) {
  const path = [];
  const visit = (node, depth) => {
    if (!node || typeof node !== 'object') {
      return false;
    }

    path.push({ node, depth });
    if (node.uniqueIdentifier === nodeId) {
      return true;
    }

    if (Array.isArray(node.children) && node.children.some((child) => visit(child, depth + 1))) {
      return true;
    }

    path.pop();
    return false;
  };

  visit(rootNode, 0);
  return path;
}

function traverseNode(node, parent, pathSegments, depth, visitor) {
  if (!node || typeof node !== 'object') {
    return;
  }

  const nextPath = [...pathSegments, normalizeNodeDisplayText(node.name)];
  visitor(node, parent, {
    pathSegments: nextPath,
    depth,
  });

  if (!Array.isArray(node.children)) {
    return;
  }

  node.children.forEach((child) => traverseNode(child, node, nextPath, depth + 1, visitor));
}

function extractUrls(value) {
  const text = typeof value === 'string' ? value : '';
  const matches = text.match(URL_PATTERN);
  return matches ? [...new Set(matches.map((match) => match.replace(/[.,;:!?]+$/g, '')))] : [];
}

function buildNodeReferenceKey(mapFilePath, nodeId) {
  const mapPath = normalizeText(mapFilePath).toLowerCase();
  const id = normalizeText(nodeId).toLowerCase();
  return mapPath && id ? `${mapPath}\u0000${id}` : '';
}

function createAnswerNode({ nodeId, answer, agent, timestamp, number }) {
  return {
    nodeType: NODE_TYPE.TEXT_BLOCK_ITEM,
    uniqueIdentifier: nodeId,
    name: answer,
    children: [],
    links: {},
    number,
    collapsed: false,
    hideDoneTasks: false,
    starred: false,
    taskState: TASK_STATE.NONE,
    metadata: {
      createdAtUtc: timestamp,
      updatedAtUtc: timestamp,
      source: `llm:${agent}`,
      device: agent,
      attachments: [],
    },
  };
}

function touchNode(node, timestamp) {
  const metadata = isPlainObject(node.metadata) ? node.metadata : {};
  node.metadata = {
    createdAtUtc: normalizeTimestamp(metadata.createdAtUtc || timestamp),
    updatedAtUtc: timestamp,
    source: typeof metadata.source === 'string' && metadata.source ? metadata.source : 'manual',
    device: typeof metadata.device === 'string' && metadata.device ? metadata.device : null,
    attachments: Array.isArray(metadata.attachments) ? metadata.attachments : [],
  };
}

function renumberChildren(node) {
  node.children.forEach((child, index) => {
    child.number = index + 1;
  });
}

function taskStateLabel(taskState) {
  switch (taskState) {
    case TASK_STATE.TODO:
      return 'todo';
    case TASK_STATE.DOING:
      return 'doing';
    case TASK_STATE.DONE:
      return 'done';
    default:
      return 'none';
  }
}

function normalizeSnapshots(snapshots) {
  return Array.isArray(snapshots)
    ? snapshots.filter((snapshot) => snapshot?.document?.rootNode && snapshot.filePath)
    : [];
}

function normalizeText(value) {
  return typeof value === 'string' ? value.trim() : '';
}

function normalizeNullableText(value) {
  const text = normalizeText(value);
  return text || null;
}

function normalizeTimestamp(value) {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? nowIso()
    : parsed.toISOString().replace(/\.\d{3}Z$/, 'Z');
}

function normalizeNullableTimestamp(value) {
  if (!value) {
    return null;
  }

  return normalizeTimestamp(value);
}

function isPlainObject(value) {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function createGuid() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID();
  }

  const timestamp = Date.now().toString(16).padStart(12, '0');
  const random = Math.random().toString(16).slice(2).padEnd(20, '0');
  return `${timestamp.slice(0, 8)}-${timestamp.slice(8, 12)}-4${random.slice(0, 3)}-8${random.slice(3, 6)}-${random.slice(6, 18)}`;
}
