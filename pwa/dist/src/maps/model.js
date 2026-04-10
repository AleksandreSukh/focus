export const TASK_STATE = Object.freeze({
  NONE: 0,
  TODO: 1,
  DOING: 2,
  DONE: 3,
});

export const NODE_TYPE = Object.freeze({
  TEXT_ITEM: 0,
  IDEA_BAG_ITEM: 1,
});

export const MAP_ERROR = Object.freeze({
  NOT_FOUND: 'NOT_FOUND',
  VALIDATION_ERROR: 'VALIDATION_ERROR',
  UNSUPPORTED_OPERATION: 'UNSUPPORTED_OPERATION',
});

const DEFAULT_DEVICE = 'focus-pwa-web';
const LEGACY_SOURCE = 'legacy-import';
const MANUAL_SOURCE = 'manual';
const CLIPBOARD_IMAGE_SOURCE = 'clipboard-image';
const UNTITLED_NODE_NAME = 'Untitled';

export function cloneMapDocument(document) {
  if (typeof structuredClone === 'function') {
    return structuredClone(document);
  }

  return JSON.parse(JSON.stringify(document));
}

export function parseMindMapDocument(content) {
  return JSON.parse(content);
}

export function serializeMindMapDocument(document) {
  return `${JSON.stringify(document, null, 2)}\n`;
}

export function normalizeMindMapDocument(rawDocument, options = {}) {
  const legacyTimestamp = normalizeTimestamp(options.fileTimestampIso || nowIso());
  const document = rawDocument && typeof rawDocument === 'object'
    ? rawDocument
    : {};

  const existingRootNode = takeCanonicalProperty(document, 'rootNode', 'RootNode');
  if (!existingRootNode || typeof existingRootNode !== 'object') {
    document.rootNode = createNode('Root', TASK_STATE.NONE, {
      timestamp: legacyTimestamp,
      source: LEGACY_SOURCE,
      device: null,
    });
  } else {
    document.rootNode = existingRootNode;
  }

  normalizeNode(document.rootNode, {
    legacyTimestamp,
    isRoot: true,
    number: 1,
  });

  // Normalize the map-level updatedAt. For old maps that lack it, fall back to
  // the root node's timestamp so the first load migrates gracefully.
  document.updatedAt = normalizeTimestamp(
    typeof document.updatedAt === 'string' && document.updatedAt
      ? document.updatedAt
      : getNodeUpdatedAt(document.rootNode) || legacyTimestamp,
  );

  return document;
}

export function buildMapSummary(snapshot) {
  const rootNode = snapshot.document?.rootNode;
  const taskCounts = getTaskCounts(snapshot.document);
  const updatedAt = snapshot.document?.updatedAt ?? getNodeUpdatedAt(rootNode);
  return {
    filePath: snapshot.filePath,
    fileName: snapshot.fileName,
    mapName: snapshot.mapName,
    rootTitle: normalizeNodeDisplayText(rootNode?.name),
    updatedAt,
    taskCounts,
  };
}

export function compareMapSummariesByRecentUpdate(left, right) {
  const timeDelta = parseTimestamp(right?.updatedAt) - parseTimestamp(left?.updatedAt);
  if (timeDelta !== 0) {
    return timeDelta;
  }

  return String(left?.fileName ?? '').localeCompare(String(right?.fileName ?? ''));
}

export function getTaskCounts(document) {
  const counts = {
    total: 0,
    open: 0,
    todo: 0,
    doing: 0,
    done: 0,
  };

  traverseDocument(document, (node, parent) => {
    if (!isTaskNode(node, parent)) {
      return;
    }

    counts.total += 1;
    if (node.taskState === TASK_STATE.TODO) {
      counts.todo += 1;
      counts.open += 1;
    } else if (node.taskState === TASK_STATE.DOING) {
      counts.doing += 1;
      counts.open += 1;
    } else if (node.taskState === TASK_STATE.DONE) {
      counts.done += 1;
    }
  });

  return counts;
}

export function collectTaskEntries(snapshot, filter = 'open') {
  const results = [];

  traverseDocument(snapshot.document, (node, parent, context) => {
    if (!isTaskNode(node, parent) || !matchesTaskFilter(node.taskState, filter)) {
      return;
    }

    results.push({
      filePath: snapshot.filePath,
      fileName: snapshot.fileName,
      mapName: snapshot.mapName,
      nodeId: node.uniqueIdentifier,
      nodeName: normalizeNodeDisplayText(node?.name),
      nodePath: context.pathSegments.join(' > '),
      nodePathSegments: [...context.pathSegments],
      taskState: node.taskState,
      depth: context.depth,
    });
  });

  return results.sort(compareTaskEntries);
}

export function findNodeRecord(document, nodeId) {
  let result = null;

  traverseDocument(document, (node, parent, context) => {
    if (node.uniqueIdentifier === nodeId) {
      result = {
        node,
        parent,
        depth: context.depth,
        pathSegments: context.pathSegments,
      };
    }
  });

  return result;
}

export function getNodeUiState(document, nodeId) {
  const record = findNodeRecord(document, nodeId);
  if (!record) {
    return null;
  }

  const { node, parent, pathSegments } = record;
  return {
    node,
    parent,
    pathSegments,
    canEditNode: node.nodeType !== NODE_TYPE.IDEA_BAG_ITEM,
    canChangeTaskState: canChangeTaskState(node, parent),
    badges: getNodeBadges(node),
  };
}

export function applyMapMutation(document, mutation) {
  const timestamp = normalizeTimestamp(mutation.timestamp || nowIso());

  switch (mutation.type) {
    case 'editNodeText':
      return editNodeText(document, mutation, timestamp);
    case 'setTaskState':
      return setNodeTaskState(document, mutation, timestamp);
    case 'addChildNote':
      return addChildNode(document, mutation, timestamp, TASK_STATE.NONE);
    case 'addChildTask':
      return addChildNode(document, mutation, timestamp, TASK_STATE.TODO);
    case 'deleteNode':
      return deleteChildNode(document, mutation, timestamp);
    case 'addAttachment':
      return addAttachmentToNode(document, mutation, timestamp);
    case 'removeAttachment':
      return removeAttachmentFromNode(document, mutation, timestamp);
    default:
      return {
        ok: false,
        error: {
          code: MAP_ERROR.UNSUPPORTED_OPERATION,
          message: `Unsupported map mutation "${mutation.type}".`,
          retriable: false,
        },
      };
  }
}

export function createMapDocument(rootName) {
  const timestamp = nowIso();
  return {
    updatedAt: timestamp,
    rootNode: {
      nodeType: NODE_TYPE.TEXT_ITEM,
      uniqueIdentifier: createGuid(),
      name: sanitizeText(String(rootName ?? '')),
      children: [],
      links: {},
      number: 1,
      collapsed: false,
      taskState: TASK_STATE.NONE,
      metadata: createMetadata(timestamp, MANUAL_SOURCE, DEFAULT_DEVICE),
    },
  };
}

export function displayTaskState(taskState) {
  switch (taskState) {
    case TASK_STATE.TODO:
      return '[ ]';
    case TASK_STATE.DOING:
      return '[~]';
    case TASK_STATE.DONE:
      return '[x]';
    default:
      return '';
  }
}

export function displayNodeName(node) {
  const marker = displayTaskState(node.taskState);
  const name = normalizeNodeDisplayText(node?.name);
  return marker ? `${marker} ${name}` : name;
}

export function normalizeNodeDisplayText(value) {
  const text = typeof value === 'string' ? value : '';
  if (!text.trim()) {
    return UNTITLED_NODE_NAME;
  }

  return text.replace(/\r\n|\r|\n/g, ' ').trim();
}

export function isClipboardImageNode(node) {
  return typeof node?.metadata?.source === 'string' && node.metadata.source === CLIPBOARD_IMAGE_SOURCE;
}

export function getNodeBadges(node) {
  const badges = [];
  if (node.nodeType === NODE_TYPE.IDEA_BAG_ITEM) {
    badges.push('Idea');
  }

  if (node.links && typeof node.links === 'object' && Object.keys(node.links).length > 0) {
    badges.push('Links');
  }

  return badges;
}

// Canonical UTC timestamp format shared with the console app: yyyy-MM-ddTHH:mm:ssZ
// No milliseconds, Z suffix — matches Newtonsoft.Json DateTimeOffset UTC serialization.
export function nowIso() {
  return formatIso(new Date());
}

function formatIso(date) {
  return date.toISOString().replace(/\.\d{3}Z$/, 'Z');
}

function normalizeNode(node, context) {
  if (!node || typeof node !== 'object') {
    return;
  }

  node.nodeType = takeCanonicalProperty(node, 'nodeType', 'NodeType');
  node.uniqueIdentifier = takeCanonicalProperty(node, 'uniqueIdentifier', 'UniqueIdentifier');
  node.name = takeCanonicalProperty(node, 'name', 'Name');
  node.children = takeCanonicalProperty(node, 'children', 'Children');
  node.links = takeCanonicalProperty(node, 'links', 'Links');
  node.number = takeCanonicalProperty(node, 'number', 'Number');
  node.collapsed = takeCanonicalProperty(node, 'collapsed', 'Collapsed');
  node.taskState = takeCanonicalProperty(node, 'taskState', 'TaskState');
  node.metadata = takeCanonicalProperty(node, 'metadata', 'Metadata');

  node.nodeType = isValidNodeType(node.nodeType) ? node.nodeType : NODE_TYPE.TEXT_ITEM;
  node.uniqueIdentifier = isValidGuid(node.uniqueIdentifier) ? node.uniqueIdentifier : createGuid();
  node.name = sanitizeText(typeof node.name === 'string' ? node.name : '');
  node.children = Array.isArray(node.children) ? node.children : [];
  node.links = normalizeLinks(node.links);
  node.number = Number.isInteger(node.number) && node.number > 0 ? node.number : context.number;
  node.collapsed = Boolean(node.collapsed);
  node.taskState = isValidTaskState(node.taskState) ? node.taskState : TASK_STATE.NONE;
  normalizeMetadata(node, context.legacyTimestamp);

  node.children.forEach((child, index) => {
    normalizeNode(child, {
      legacyTimestamp: context.legacyTimestamp,
      isRoot: false,
      number: index + 1,
    });
  });
}

function normalizeMetadata(node, legacyTimestamp) {
  if (!node.metadata || typeof node.metadata !== 'object') {
    node.metadata = createMetadata(legacyTimestamp, LEGACY_SOURCE, null);
    return;
  }

  node.metadata.createdAtUtc = takeCanonicalProperty(node.metadata, 'createdAtUtc', 'CreatedAtUtc');
  node.metadata.updatedAtUtc = takeCanonicalProperty(node.metadata, 'updatedAtUtc', 'UpdatedAtUtc');
  node.metadata.source = takeCanonicalProperty(node.metadata, 'source', 'Source');
  node.metadata.device = takeCanonicalProperty(node.metadata, 'device', 'Device');
  node.metadata.attachments = takeCanonicalProperty(node.metadata, 'attachments', 'Attachments');

  node.metadata.createdAtUtc = normalizeTimestamp(node.metadata.createdAtUtc || legacyTimestamp);
  node.metadata.updatedAtUtc = normalizeTimestamp(node.metadata.updatedAtUtc || node.metadata.createdAtUtc);
  node.metadata.source =
    typeof node.metadata.source === 'string' && node.metadata.source
      ? node.metadata.source
      : MANUAL_SOURCE;
  node.metadata.device =
    typeof node.metadata.device === 'string' && node.metadata.device
      ? node.metadata.device
      : null;
  node.metadata.attachments = Array.isArray(node.metadata.attachments)
    ? node.metadata.attachments.map((attachment) => normalizeAttachment(attachment, legacyTimestamp))
    : [];
}

function normalizeAttachment(attachment, legacyTimestamp) {
  const normalized = attachment && typeof attachment === 'object'
    ? attachment
    : {};

  normalized.id = takeCanonicalProperty(normalized, 'id', 'Id');
  normalized.relativePath = takeCanonicalProperty(normalized, 'relativePath', 'RelativePath');
  normalized.mediaType = takeCanonicalProperty(normalized, 'mediaType', 'MediaType');
  normalized.displayName = takeCanonicalProperty(normalized, 'displayName', 'DisplayName');
  normalized.createdAtUtc = takeCanonicalProperty(normalized, 'createdAtUtc', 'CreatedAtUtc');

  normalized.id = isValidGuid(normalized.id) ? normalized.id : createGuid();
  normalized.relativePath = typeof normalized.relativePath === 'string' ? normalized.relativePath : '';
  normalized.mediaType = typeof normalized.mediaType === 'string' ? normalized.mediaType : '';
  normalized.displayName = typeof normalized.displayName === 'string' ? normalized.displayName : '';
  normalized.createdAtUtc = normalizeTimestamp(normalized.createdAtUtc || legacyTimestamp);
  return normalized;
}

function editNodeText(document, mutation, timestamp) {
  const normalizedText = normalizeInputText(mutation.text);
  if (!normalizedText) {
    return validationError('Node text cannot be empty.');
  }

  const record = findNodeRecord(document, mutation.nodeId);
  if (!record) {
    return notFoundError(mutation.nodeId);
  }

  if (record.node.nodeType === NODE_TYPE.IDEA_BAG_ITEM) {
    return validationError('Idea-tag nodes are read-only in the PWA.');
  }

  record.node.name = normalizedText;
  touchMetadata(record.node, timestamp);
  touchDocumentTimestamp(document, timestamp);
  return {
    ok: true,
    value: {
      affectedNodeId: record.node.uniqueIdentifier,
      selectedNodeId: record.node.uniqueIdentifier,
    },
  };
}

function setNodeTaskState(document, mutation, timestamp) {
  const record = findNodeRecord(document, mutation.nodeId);
  if (!record) {
    return notFoundError(mutation.nodeId);
  }

  const validation = validateTaskTarget(record.node, record.parent);
  if (validation) {
    return validation;
  }

  record.node.taskState = isValidTaskState(mutation.taskState)
    ? mutation.taskState
    : TASK_STATE.NONE;
  touchMetadata(record.node, timestamp);
  touchDocumentTimestamp(document, timestamp);
  return {
    ok: true,
    value: {
      affectedNodeId: record.node.uniqueIdentifier,
      selectedNodeId: record.node.uniqueIdentifier,
    },
  };
}

function addChildNode(document, mutation, timestamp, taskState) {
  const normalizedText = normalizeInputText(mutation.text);
  if (!normalizedText) {
    return validationError('Child node text cannot be empty.');
  }

  const parentRecord = findNodeRecord(document, mutation.parentNodeId);
  if (!parentRecord) {
    return notFoundError(mutation.parentNodeId);
  }

  if (parentRecord.node.nodeType === NODE_TYPE.IDEA_BAG_ITEM) {
    return validationError('Idea-tag nodes are read-only in the PWA.');
  }

  const childNode = createNode(normalizedText, taskState, {
    nodeId: mutation.newNodeId,
    timestamp,
    source: MANUAL_SOURCE,
    device: DEFAULT_DEVICE,
    number: parentRecord.node.children.length + 1,
  });

  parentRecord.node.children.push(childNode);
  renumberChildren(parentRecord.node);
  touchMetadata(parentRecord.node, timestamp);
  touchDocumentTimestamp(document, timestamp);

  return {
    ok: true,
    value: {
      affectedNodeId: childNode.uniqueIdentifier,
      selectedNodeId: childNode.uniqueIdentifier,
    },
  };
}

function deleteChildNode(document, mutation, timestamp) {
  const record = findNodeRecord(document, mutation.nodeId);
  if (!record) {
    return notFoundError(mutation.nodeId);
  }

  if (!record.parent) {
    return validationError('Cannot delete the root node.');
  }

  if (record.node.nodeType === NODE_TYPE.IDEA_BAG_ITEM) {
    return validationError('Idea-tag nodes cannot be deleted in the PWA.');
  }

  const parent = record.parent;
  const index = parent.children.indexOf(record.node);
  if (index === -1) {
    return validationError('Node was not found in parent children.');
  }

  parent.children.splice(index, 1);
  renumberChildren(parent);
  touchMetadata(parent, timestamp);
  touchDocumentTimestamp(document, timestamp);

  return {
    ok: true,
    value: {
      affectedNodeId: parent.uniqueIdentifier,
      selectedNodeId: parent.uniqueIdentifier,
    },
  };
}

function addAttachmentToNode(document, mutation, timestamp) {
  const record = findNodeRecord(document, mutation.nodeId);
  if (!record) {
    return notFoundError(mutation.nodeId);
  }

  const attachment = normalizeAttachment(mutation.attachment || {}, timestamp);
  if (!attachment.relativePath) {
    return validationError('Attachment must have a relativePath.');
  }

  normalizeMetadata(record.node, timestamp);
  record.node.metadata.attachments = record.node.metadata.attachments.filter(
    (a) => a.id !== attachment.id,
  );
  record.node.metadata.attachments.push(attachment);
  touchMetadata(record.node, timestamp);
  touchDocumentTimestamp(document, timestamp);
  return {
    ok: true,
    value: {
      affectedNodeId: record.node.uniqueIdentifier,
      selectedNodeId: record.node.uniqueIdentifier,
    },
  };
}

function removeAttachmentFromNode(document, mutation, timestamp) {
  const record = findNodeRecord(document, mutation.nodeId);
  if (!record) {
    return notFoundError(mutation.nodeId);
  }

  normalizeMetadata(record.node, timestamp);
  const before = record.node.metadata.attachments.length;
  record.node.metadata.attachments = record.node.metadata.attachments.filter(
    (a) => a.id !== mutation.attachmentId,
  );

  if (record.node.metadata.attachments.length === before) {
    return validationError(`Attachment "${mutation.attachmentId}" not found on node.`);
  }

  touchMetadata(record.node, timestamp);
  touchDocumentTimestamp(document, timestamp);
  return {
    ok: true,
    value: {
      affectedNodeId: record.node.uniqueIdentifier,
      selectedNodeId: record.node.uniqueIdentifier,
    },
  };
}

function validateTaskTarget(node, parent) {
  if (!parent) {
    return validationError("Can't change task state for root node");
  }

  if (node.nodeType === NODE_TYPE.IDEA_BAG_ITEM) {
    return validationError('Task mode is not supported for idea tags');
  }

  return null;
}

function createNode(name, taskState, options = {}) {
  const timestamp = normalizeTimestamp(options.timestamp || nowIso());
  return {
    nodeType: NODE_TYPE.TEXT_ITEM,
    uniqueIdentifier: options.nodeId || createGuid(),
    name: sanitizeText(name),
    children: [],
    links: {},
    number: options.number || 1,
    collapsed: false,
    taskState,
    metadata: createMetadata(timestamp, options.source || MANUAL_SOURCE, options.device ?? DEFAULT_DEVICE),
  };
}

function createMetadata(timestamp, source, device) {
  return {
    createdAtUtc: timestamp,
    updatedAtUtc: timestamp,
    source,
    device,
    attachments: [],
  };
}

function touchMetadata(node, timestamp) {
  normalizeMetadata(node, timestamp);
  node.metadata.updatedAtUtc = normalizeTimestamp(timestamp);
}

function touchDocumentTimestamp(document, timestamp) {
  document.updatedAt = normalizeTimestamp(timestamp);
}

function renumberChildren(node) {
  node.children.forEach((child, index) => {
    child.number = index + 1;
  });
}

function traverseDocument(document, visitor) {
  const rootNode = document?.rootNode;
  if (!rootNode || typeof rootNode !== 'object') {
    return;
  }

  traverseNode(rootNode, null, [], 0, visitor);
}

function traverseNode(node, parent, pathSegments, depth, visitor) {
  const nextPath = [...pathSegments, normalizeNodeDisplayText(node?.name)];
  visitor(node, parent, {
    pathSegments: nextPath,
    depth,
  });

  if (!Array.isArray(node.children)) {
    return;
  }

  node.children.forEach((child) => {
    traverseNode(child, node, nextPath, depth + 1, visitor);
  });
}

function matchesTaskFilter(taskState, filter) {
  switch (filter) {
    case 'all':
      return taskState !== TASK_STATE.NONE;
    case 'todo':
      return taskState === TASK_STATE.TODO;
    case 'doing':
      return taskState === TASK_STATE.DOING;
    case 'done':
      return taskState === TASK_STATE.DONE;
    case 'open':
    default:
      return taskState === TASK_STATE.TODO || taskState === TASK_STATE.DOING;
  }
}

function compareTaskEntries(left, right) {
  const priorityDelta = taskSortPriority(left.taskState) - taskSortPriority(right.taskState);
  if (priorityDelta !== 0) {
    return priorityDelta;
  }

  const mapDelta = left.mapName.localeCompare(right.mapName);
  if (mapDelta !== 0) {
    return mapDelta;
  }

  return left.nodePath.localeCompare(right.nodePath);
}

function taskSortPriority(taskState) {
  switch (taskState) {
    case TASK_STATE.DOING:
      return 0;
    case TASK_STATE.TODO:
      return 1;
    case TASK_STATE.DONE:
      return 2;
    default:
      return 3;
  }
}

function canChangeTaskState(node, parent) {
  return Boolean(
    parent &&
    node &&
    node.nodeType !== NODE_TYPE.IDEA_BAG_ITEM &&
    isValidGuid(node.uniqueIdentifier) &&
    isValidTaskState(node.taskState),
  );
}

function isTaskNode(node, parent) {
  return Boolean(
    canChangeTaskState(node, parent) &&
    node.taskState !== TASK_STATE.NONE,
  );
}

function getNodeUpdatedAt(node) {
  return typeof node?.metadata?.updatedAtUtc === 'string'
    ? node.metadata.updatedAtUtc
    : null;
}

function normalizeInputText(text) {
  return sanitizeText(String(text ?? '').trim());
}

function sanitizeText(input) {
  return String(input ?? '')
    .split('')
    .filter((character) => {
      const code = character.charCodeAt(0);
      return code >= 32 || character === '\r' || character === '\n' || character === '\t';
    })
    .join('');
}

function normalizeTimestamp(value) {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? nowIso()
    : formatIso(parsed);
}

function parseTimestamp(value) {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? 0 : parsed.getTime();
}

function isValidTaskState(value) {
  return [TASK_STATE.NONE, TASK_STATE.TODO, TASK_STATE.DOING, TASK_STATE.DONE].includes(value);
}

function isValidNodeType(value) {
  return value === NODE_TYPE.TEXT_ITEM || value === NODE_TYPE.IDEA_BAG_ITEM;
}

function isPlainObject(value) {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function normalizeLinks(links) {
  if (!isPlainObject(links)) {
    return {};
  }

  Object.values(links).forEach((link) => {
    if (!isPlainObject(link)) {
      return;
    }

    link.id = takeCanonicalProperty(link, 'id', 'Id');
    link.relationType = takeCanonicalProperty(link, 'relationType', 'RelationType');
    link.metadata = takeCanonicalProperty(link, 'metadata', 'Metadata');
  });

  return links;
}

function isValidGuid(value) {
  return typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

function createGuid() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID();
  }

  const timestamp = Date.now().toString(16).padStart(12, '0');
  const random = Math.random().toString(16).slice(2).padEnd(20, '0');
  return `${timestamp.slice(0, 8)}-${timestamp.slice(8, 12)}-4${random.slice(0, 3)}-8${random.slice(3, 6)}-${random.slice(6, 18)}`;
}

function takeCanonicalProperty(target, preferredKey, legacyKey) {
  if (!isPlainObject(target)) {
    return undefined;
  }

  if (target[preferredKey] !== undefined) {
    if (preferredKey !== legacyKey && legacyKey in target) {
      delete target[legacyKey];
    }
    return target[preferredKey];
  }

  if (!(legacyKey in target)) {
    return undefined;
  }

  const value = target[legacyKey];
  target[preferredKey] = value;
  if (preferredKey !== legacyKey) {
    delete target[legacyKey];
  }
  return value;
}

function validationError(message) {
  return {
    ok: false,
    error: {
      code: MAP_ERROR.VALIDATION_ERROR,
      message,
      retriable: false,
    },
  };
}

function notFoundError(nodeId) {
  return {
    ok: false,
    error: {
      code: MAP_ERROR.NOT_FOUND,
      message: `Node "${nodeId}" was not found.`,
      retriable: false,
    },
  };
}
