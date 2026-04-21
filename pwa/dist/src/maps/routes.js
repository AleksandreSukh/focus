import { resolveVisibleNodeId } from './model.js';

export const HASH_ROUTE = {
  maps: '#maps',
  tasks: '#tasks',
};

function normalizeNodeId(nodeId) {
  return typeof nodeId === 'string' ? nodeId.trim() : '';
}

export function getDocumentRootNodeId(document) {
  return document?.rootNode?.uniqueIdentifier || '';
}

export function resolveViewportNodeId(document, nodeId = '') {
  const rootNodeId = getDocumentRootNodeId(document);
  if (!rootNodeId) {
    return '';
  }

  const requestedNodeId = normalizeNodeId(nodeId) || rootNodeId;
  return resolveVisibleNodeId(document, requestedNodeId) || rootNodeId;
}

export function isSubtreeNodeSelection(document, nodeId = '') {
  const rootNodeId = getDocumentRootNodeId(document);
  const viewportNodeId = resolveViewportNodeId(document, nodeId);
  return Boolean(rootNodeId && viewportNodeId && viewportNodeId !== rootNodeId);
}

export function shouldShowWorkspaceTabs(currentView, document, nodeId = '') {
  return currentView !== 'map' || !isSubtreeNodeSelection(document, nodeId);
}

export function buildHashRoute(view, mapPath = '', nodeId = '', rootNodeId = '') {
  if (view === 'tasks') {
    return HASH_ROUTE.tasks;
  }

  if (view === 'map' && mapPath) {
    const baseHash = `#map/${encodeURIComponent(mapPath)}`;
    const normalizedNodeId = normalizeNodeId(nodeId);
    const normalizedRootNodeId = normalizeNodeId(rootNodeId);
    if (normalizedNodeId && normalizedNodeId !== normalizedRootNodeId) {
      return `${baseHash}?node=${encodeURIComponent(normalizedNodeId)}`;
    }

    return baseHash;
  }

  return HASH_ROUTE.maps;
}

export function parseHashRoute(hashValue) {
  const normalizedHash = typeof hashValue === 'string' ? hashValue : '';
  const routeText = normalizedHash.startsWith('#') ? normalizedHash.slice(1) : normalizedHash;

  if (!routeText || routeText === 'maps') {
    return { view: 'maps', nodeId: '', isInvalid: false };
  }

  if (routeText === 'tasks') {
    return { view: 'tasks', nodeId: '', isInvalid: false };
  }

  if (routeText.startsWith('map/')) {
    const rawMapRoute = routeText.slice(4);
    const queryIndex = rawMapRoute.indexOf('?');
    const encodedPath = queryIndex >= 0 ? rawMapRoute.slice(0, queryIndex) : rawMapRoute;
    const queryText = queryIndex >= 0 ? rawMapRoute.slice(queryIndex + 1) : '';

    if (!encodedPath) {
      return { view: 'maps', nodeId: '', isInvalid: true };
    }

    try {
      const params = new URLSearchParams(queryText);
      return {
        view: 'map',
        mapPath: decodeURIComponent(encodedPath),
        nodeId: normalizeNodeId(params.get('node') || ''),
        isInvalid: false,
      };
    } catch (error) {
      return { view: 'maps', nodeId: '', isInvalid: true };
    }
  }

  return { view: 'maps', nodeId: '', isInvalid: true };
}
