const LEGACY_SOURCE = 'legacy-import';

export function hasConflictMarkers(content) {
  return typeof content === 'string' && content.includes('<<<<<<< ');
}

export function tryResolveMapConflict(conflictedContent) {
  if (!hasConflictMarkers(conflictedContent)) {
    return {
      ok: false,
      resolvedContent: null,
    };
  }

  const ours = buildResolvedContent(conflictedContent, true);
  const theirs = buildResolvedContent(conflictedContent, false);
  const merged = tryMergeResolve(ours, theirs);
  if (merged.ok) {
    return merged;
  }

  const oursTimestamp = tryParseMapTimestamp(ours);
  const theirsTimestamp = tryParseMapTimestamp(theirs);
  if (oursTimestamp === null && theirsTimestamp === null) {
    return {
      ok: false,
      resolvedContent: null,
    };
  }

  return {
    ok: true,
    resolvedContent: oursTimestamp !== null && theirsTimestamp !== null
      ? (oursTimestamp > theirsTimestamp ? ours : theirs)
      : (oursTimestamp !== null ? ours : theirs),
  };
}

function tryMergeResolve(oursJson, theirsJson) {
  try {
    const ours = JSON.parse(oursJson);
    const theirs = JSON.parse(theirsJson);
    if (!isPlainObject(ours) || !isPlainObject(theirs)) {
      return {
        ok: false,
        resolvedContent: null,
      };
    }

    if (!tryMergeMapDocument(ours, theirs)) {
      return {
        ok: false,
        resolvedContent: null,
      };
    }

    return {
      ok: true,
      resolvedContent: JSON.stringify(ours, null, 2),
    };
  } catch {
    return {
      ok: false,
      resolvedContent: null,
    };
  }
}

function tryMergeMapDocument(ours, theirs) {
  for (const key of getAllKeys(ours, theirs)) {
    const oursValue = ours[key];
    const theirsValue = theirs[key];

    if (deepEqual(oursValue, theirsValue)) {
      continue;
    }

    if (oursValue === undefined) {
      ours[key] = deepClone(theirsValue);
      continue;
    }

    if (theirsValue === undefined) {
      continue;
    }

    switch (key) {
      case 'updatedAt':
        mergeTimestampMax(ours, theirs, key);
        break;
      case 'rootNode':
        if (!isPlainObject(oursValue) || !isPlainObject(theirsValue)) {
          return false;
        }
        if (!tryMergeNode(oursValue, theirsValue)) {
          return false;
        }
        break;
      default:
        return false;
    }
  }

  return true;
}

function tryMergeNode(ours, theirs) {
  if (!deepEqual(ours.uniqueIdentifier, theirs.uniqueIdentifier)) {
    return false;
  }

  if (!deepEqual(ours.nodeType, theirs.nodeType)) {
    return false;
  }

  const oursUpdated = tryParseTimestamp(ours?.metadata?.updatedAtUtc);
  const theirsUpdated = tryParseTimestamp(theirs?.metadata?.updatedAtUtc);
  const theirsIsNewerOrEqual = compareNullableTimestamps(theirsUpdated, oursUpdated) >= 0;

  for (const key of getAllKeys(ours, theirs)) {
    const oursValue = ours[key];
    const theirsValue = theirs[key];

    if (deepEqual(oursValue, theirsValue)) {
      continue;
    }

    if (oursValue === undefined) {
      ours[key] = deepClone(theirsValue);
      continue;
    }

    if (theirsValue === undefined) {
      continue;
    }

    switch (key) {
      case 'uniqueIdentifier':
      case 'nodeType':
        return false;
      case 'number':
        break;
      case 'name':
      case 'collapsed':
      case 'hideDoneTasks':
      case 'hideDoneTasksExplicit':
      case 'starred':
      case 'taskState':
        if (theirsIsNewerOrEqual) {
          ours[key] = deepClone(theirsValue);
        }
        break;
      case 'metadata':
        if (!isPlainObject(oursValue) || !isPlainObject(theirsValue)) {
          return false;
        }
        if (!tryMergeMetadata(oursValue, theirsValue, theirsIsNewerOrEqual)) {
          return false;
        }
        break;
      case 'links':
        if (!isPlainObject(oursValue) || !isPlainObject(theirsValue)) {
          return false;
        }
        mergeLinksUnion(oursValue, theirsValue);
        break;
      case 'children':
        if (!Array.isArray(oursValue) || !Array.isArray(theirsValue)) {
          return false;
        }
        if (!tryMergeChildren(oursValue, theirsValue)) {
          return false;
        }
        break;
      default:
        return false;
    }
  }

  return true;
}

function tryMergeMetadata(ours, theirs, theirsIsNewerOrEqual) {
  for (const key of getAllKeys(ours, theirs)) {
    const oursValue = ours[key];
    const theirsValue = theirs[key];

    if (deepEqual(oursValue, theirsValue)) {
      continue;
    }

    if (oursValue === undefined) {
      ours[key] = deepClone(theirsValue);
      continue;
    }

    if (theirsValue === undefined) {
      continue;
    }

    switch (key) {
      case 'updatedAtUtc':
        mergeTimestampMax(ours, theirs, key);
        break;
      case 'createdAtUtc':
        mergeTimestampMin(ours, theirs, key);
        break;
      case 'source': {
        const oursSource = typeof oursValue === 'string' ? oursValue : '';
        const theirsSource = typeof theirsValue === 'string' ? theirsValue : '';
        const takeTheirs = (oursSource === LEGACY_SOURCE && theirsSource !== LEGACY_SOURCE)
          || (theirsIsNewerOrEqual && theirsSource !== LEGACY_SOURCE);
        if (takeTheirs) {
          ours[key] = deepClone(theirsValue);
        }
        break;
      }
      case 'device':
        if (theirsIsNewerOrEqual) {
          ours[key] = deepClone(theirsValue);
        }
        break;
      case 'attachments':
        if (!Array.isArray(oursValue) || !Array.isArray(theirsValue)) {
          return false;
        }
        mergeAttachmentsUnion(oursValue, theirsValue);
        break;
      default:
        return false;
    }
  }

  return true;
}

function tryMergeChildren(oursChildren, theirsChildren) {
  const oursById = new Map();
  for (const child of oursChildren) {
    if (!isPlainObject(child)) {
      return false;
    }

    const id = typeof child.uniqueIdentifier === 'string' ? child.uniqueIdentifier : '';
    if (!id) {
      return false;
    }

    oursById.set(id.toLowerCase(), child);
  }

  for (const child of theirsChildren) {
    if (!isPlainObject(child)) {
      return false;
    }

    const id = typeof child.uniqueIdentifier === 'string' ? child.uniqueIdentifier : '';
    if (!id) {
      return false;
    }

    const oursChild = oursById.get(id.toLowerCase());
    if (oursChild) {
      if (!tryMergeNode(oursChild, child)) {
        return false;
      }
      continue;
    }

    oursChildren.push(deepClone(child));
  }

  let number = 1;
  for (const child of oursChildren) {
    if (isPlainObject(child)) {
      child.number = number;
    }
    number += 1;
  }

  return true;
}

function mergeLinksUnion(oursLinks, theirsLinks) {
  for (const [key, value] of Object.entries(theirsLinks)) {
    if (oursLinks[key] === undefined) {
      oursLinks[key] = deepClone(value);
    }
  }
}

function mergeAttachmentsUnion(oursAttachments, theirsAttachments) {
  const oursIds = new Set();
  for (const attachment of oursAttachments) {
    if (!isPlainObject(attachment)) {
      continue;
    }

    const id = typeof attachment.id === 'string' ? attachment.id : '';
    if (id) {
      oursIds.add(id.toLowerCase());
    }
  }

  for (const attachment of theirsAttachments) {
    if (!isPlainObject(attachment)) {
      continue;
    }

    const id = typeof attachment.id === 'string' ? attachment.id : '';
    if (id && !oursIds.has(id.toLowerCase())) {
      oursAttachments.push(deepClone(attachment));
      oursIds.add(id.toLowerCase());
    }
  }
}

function mergeTimestampMax(ours, theirs, key) {
  const oursTimestamp = tryParseTimestamp(ours[key]);
  const theirsTimestamp = tryParseTimestamp(theirs[key]);
  if (compareNullableTimestamps(theirsTimestamp, oursTimestamp) > 0) {
    ours[key] = deepClone(theirs[key]);
  }
}

function mergeTimestampMin(ours, theirs, key) {
  const oursTimestamp = tryParseTimestamp(ours[key]);
  const theirsTimestamp = tryParseTimestamp(theirs[key]);
  if (theirsTimestamp !== null && compareNullableTimestamps(theirsTimestamp, oursTimestamp) < 0) {
    ours[key] = deepClone(theirs[key]);
  }
}

function buildResolvedContent(content, takeOurs) {
  const normalized = content.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
  const lines = normalized.split('\n');
  const result = [];
  let inOurs = false;
  let inTheirs = false;

  for (const line of lines) {
    if (line.startsWith('<<<<<<< ')) {
      inOurs = true;
      inTheirs = false;
      continue;
    }

    if (line === '=======') {
      inOurs = false;
      inTheirs = true;
      continue;
    }

    if (line.startsWith('>>>>>>> ')) {
      inOurs = false;
      inTheirs = false;
      continue;
    }

    if (!inOurs && !inTheirs) {
      result.push(line);
      continue;
    }

    if (takeOurs && inOurs) {
      result.push(line);
      continue;
    }

    if (!takeOurs && inTheirs) {
      result.push(line);
    }
  }

  return result.join('\n');
}

function tryParseMapTimestamp(jsonContent) {
  if (typeof jsonContent !== 'string' || !jsonContent.trim()) {
    return null;
  }

  try {
    const document = JSON.parse(jsonContent);
    if (!isPlainObject(document)) {
      return null;
    }

    const updatedAt = tryParseTimestamp(document.updatedAt);
    if (updatedAt !== null) {
      return updatedAt;
    }

    return tryParseTimestamp(document?.rootNode?.metadata?.updatedAtUtc);
  } catch {
    return null;
  }
}

function tryParseTimestamp(value) {
  if (typeof value !== 'string' || !value.trim()) {
    return null;
  }

  const timestamp = Date.parse(value);
  return Number.isNaN(timestamp) ? null : timestamp;
}

function compareNullableTimestamps(left, right) {
  if (left === right) {
    return 0;
  }

  if (left === null) {
    return -1;
  }

  if (right === null) {
    return 1;
  }

  return left - right;
}

function getAllKeys(ours, theirs) {
  return new Set([
    ...Object.keys(ours),
    ...Object.keys(theirs),
  ]);
}

function deepClone(value) {
  if (value === undefined) {
    return undefined;
  }

  return JSON.parse(JSON.stringify(value));
}

function deepEqual(left, right) {
  if (left === right) {
    return true;
  }

  if (left === null || right === null) {
    return left === right;
  }

  if (Array.isArray(left) || Array.isArray(right)) {
    if (!Array.isArray(left) || !Array.isArray(right) || left.length !== right.length) {
      return false;
    }

    return left.every((value, index) => deepEqual(value, right[index]));
  }

  if (isPlainObject(left) && isPlainObject(right)) {
    const leftKeys = Object.keys(left);
    const rightKeys = Object.keys(right);
    if (leftKeys.length !== rightKeys.length) {
      return false;
    }

    return leftKeys.every((key) => Object.prototype.hasOwnProperty.call(right, key) && deepEqual(left[key], right[key]));
  }

  return false;
}

function isPlainObject(value) {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
