using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Application;

public interface ILinkIndex
{
    bool HasQueuedLinkSources { get; }

    IReadOnlyCollection<Node> QueuedLinkSources { get; }

    void Rebuild(IMapRepository mapRepository);

    void QueueLinkSource(Node node);

    Node PeekQueuedLinkSource();

    Node PopQueuedLinkSource();

    bool TryGetNode(Guid nodeIdentifier, out Node node);

    bool TryGetNodeFile(Guid nodeIdentifier, out string mapFilePath);

    bool TryGetBacklinkIds(Guid nodeIdentifier, out IReadOnlyCollection<Guid> backlinkIds);
}
