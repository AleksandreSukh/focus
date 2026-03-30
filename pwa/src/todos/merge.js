const REMOTE_TIEBREAKER = 1;

function byId(items) {
  return new Map(items.map((item) => [item.id, item]));
}

function parseTime(value) {
  const timestamp = Date.parse(value);
  return Number.isNaN(timestamp) ? 0 : timestamp;
}

function preferRemote(remote, local) {
  const remoteTime = parseTime(remote.updatedAt);
  const localTime = parseTime(local.updatedAt);

  if (remoteTime > localTime) {
    return { ...remote };
  }

  if (localTime > remoteTime) {
    return { ...local };
  }

  return REMOTE_TIEBREAKER > 0 ? { ...remote } : { ...local };
}

export function mergeTodo(remote, local) {
  const winner = preferRemote(remote, local);
  return {
    id: winner.id,
    text: winner.text,
    completed: winner.completed,
    deleted: winner.deleted ?? false,
    createdAt: remote.createdAt ?? local.createdAt,
    updatedAt: winner.updatedAt,
  };
}

export function mergeDocuments(remote, local) {
  const remoteItems = byId(remote.items);
  const localItems = byId(local.items);
  const allIds = new Set([...remoteItems.keys(), ...localItems.keys()]);
  const mergedItems = [];

  for (const id of allIds) {
    const remoteItem = remoteItems.get(id);
    const localItem = localItems.get(id);

    if (remoteItem && localItem) {
      mergedItems.push(mergeTodo(remoteItem, localItem));
      continue;
    }

    if (remoteItem) {
      mergedItems.push({ ...remoteItem });
      continue;
    }

    if (localItem) {
      mergedItems.push({ ...localItem });
    }
  }

  mergedItems.sort((a, b) => {
    const delta = parseTime(b.updatedAt) - parseTime(a.updatedAt);
    if (delta !== 0) {
      return delta;
    }

    return a.id.localeCompare(b.id);
  });

  return {
    version: Math.max(remote.version ?? 1, local.version ?? 1),
    items: mergedItems,
  };
}
