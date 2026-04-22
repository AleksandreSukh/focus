import { findNodeRecord, normalizeNodeDisplayText } from './model.js';

const RELATION_LABELS = Object.freeze({
  0: 'relates',
  1: 'prerequisite',
  2: 'todo-with',
  3: 'causes',
});

export function collectOutgoingRelatedNodeEntries(node, snapshots) {
  const relatedEntries = [];
  const snapshotList = normalizeSnapshots(snapshots);
  const links = getNormalizedLinks(node);

  links.forEach((link) => {
    const linkedNodeId = normalizeNodeId(link.id);
    if (!linkedNodeId) {
      return;
    }

    const resolved = findNodeAcrossSnapshots(snapshotList, linkedNodeId);
    if (!resolved) {
      return;
    }

    relatedEntries.push(buildRelatedNodeEntry(
      resolved.snapshot,
      resolved.record.node,
      resolved.record.pathSegments,
      linkedNodeId,
      'outgoing',
      formatOutgoingRelationLabel(link.relationType),
    ));
  });

  return relatedEntries.sort(compareRelatedNodeEntries);
}

export function collectBacklinkRelatedNodeEntries(targetNodeId, snapshots) {
  const relatedEntries = [];
  const normalizedTargetId = normalizeNodeId(targetNodeId);
  if (!normalizedTargetId) {
    return relatedEntries;
  }

  const snapshotList = normalizeSnapshots(snapshots);
  snapshotList.forEach((snapshot) => {
    traverseSnapshotNodes(snapshot, (node, pathSegments) => {
      getNormalizedLinks(node).forEach((link) => {
        if (normalizeNodeId(link.id) !== normalizedTargetId) {
          return;
        }

        relatedEntries.push(buildRelatedNodeEntry(
          snapshot,
          node,
          pathSegments,
          normalizeNodeId(node?.uniqueIdentifier),
          'backlink',
          formatBacklinkRelationLabel(link.relationType),
        ));
      });
    });
  });

  return relatedEntries.sort(compareRelatedNodeEntries);
}

export function formatOutgoingRelationLabel(relationType) {
  const normalized = normalizeRelationType(relationType);
  return Object.prototype.hasOwnProperty.call(RELATION_LABELS, normalized)
    ? RELATION_LABELS[normalized]
    : 'link';
}

export function formatBacklinkRelationLabel(relationType) {
  const outgoingLabel = formatOutgoingRelationLabel(relationType);
  return outgoingLabel === 'link'
    ? 'backlink'
    : `backlink: ${outgoingLabel}`;
}

function normalizeSnapshots(snapshots) {
  return Array.isArray(snapshots)
    ? snapshots.filter((snapshot) => snapshot?.filePath && snapshot?.document)
    : [];
}

function getNormalizedLinks(node) {
  if (!node?.links || typeof node.links !== 'object') {
    return [];
  }

  return Object.values(node.links).filter((link) => link && typeof link.id === 'string');
}

function findNodeAcrossSnapshots(snapshots, nodeId) {
  for (const snapshot of snapshots) {
    const record = findNodeRecord(snapshot.document, nodeId);
    if (record) {
      return { snapshot, record };
    }
  }

  return null;
}

function buildRelatedNodeEntry(snapshot, node, pathSegments, nodeId, direction, relationLabel) {
  const normalizedPathSegments = Array.isArray(pathSegments)
    ? pathSegments.map((segment) => normalizeNodeDisplayText(segment))
    : [];
  return {
    direction,
    mapPath: snapshot.filePath,
    mapName: snapshot.mapName,
    nodeId,
    nodeName: normalizeNodeDisplayText(node?.name),
    nodePath: normalizedPathSegments.join(' > '),
    nodePathSegments: normalizedPathSegments,
    relationLabel,
  };
}

function compareRelatedNodeEntries(left, right) {
  const mapDelta = String(left?.mapName ?? '').localeCompare(String(right?.mapName ?? ''));
  if (mapDelta !== 0) {
    return mapDelta;
  }

  return String(left?.nodePath ?? '').localeCompare(String(right?.nodePath ?? ''));
}

function traverseSnapshotNodes(snapshot, visitor) {
  const rootNode = snapshot?.document?.rootNode;
  if (!rootNode || typeof rootNode !== 'object') {
    return;
  }

  traverseNode(rootNode, [], visitor);
}

function traverseNode(node, pathSegments, visitor) {
  const nextPathSegments = [...pathSegments, normalizeNodeDisplayText(node?.name)];
  visitor(node, nextPathSegments);

  if (!Array.isArray(node?.children)) {
    return;
  }

  node.children.forEach((childNode) => {
    traverseNode(childNode, nextPathSegments, visitor);
  });
}

function normalizeNodeId(nodeId) {
  return typeof nodeId === 'string' ? nodeId.trim() : '';
}

function normalizeRelationType(relationType) {
  if (Number.isInteger(relationType)) {
    return relationType;
  }

  const parsed = Number.parseInt(String(relationType ?? ''), 10);
  return Number.isInteger(parsed) ? parsed : -1;
}
