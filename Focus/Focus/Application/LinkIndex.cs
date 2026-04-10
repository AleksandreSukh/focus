using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Application;

internal sealed class LinkIndex : ILinkIndex
{
    private readonly Dictionary<Guid, Node> _nodes = new();
    private readonly Dictionary<Guid, string> _nodeFiles = new();
    private readonly Dictionary<Guid, HashSet<Guid>> _backlinks = new();
    private readonly Stack<Node> _queuedLinkSources = new();

    public bool HasQueuedLinkSources => _queuedLinkSources.Count > 0;

    public IReadOnlyCollection<Node> QueuedLinkSources => _queuedLinkSources.ToArray();

    public Node PeekQueuedLinkSource() => _queuedLinkSources.Peek();

    public Node PopQueuedLinkSource() => _queuedLinkSources.Pop();

    public void QueueLinkSource(Node node)
    {
        _queuedLinkSources.Push(node);
    }

    public void Rebuild(IMapRepository mapRepository)
    {
        _nodes.Clear();
        _nodeFiles.Clear();
        _backlinks.Clear();
        _queuedLinkSources.Clear();

        var usedIdentifiers = new HashSet<Guid>();
        var loadedMaps = new List<(string FilePath, MindMap Map)>();

        foreach (var file in mapRepository.GetAll())
        {
            var map = mapRepository.OpenMap(file.FullName, usedIdentifiers);
            IndexMap(map, file.FullName);
            loadedMaps.Add((file.FullName, map));
        }

        var liveGuids = new HashSet<Guid>(_nodes.Keys);
        foreach (var (filePath, map) in loadedMaps)
        {
            if (map.ScrubDeadLinks(liveGuids))
                mapRepository.SaveMap(filePath, map);
        }
    }

    public bool TryGetBacklinkIds(Guid nodeIdentifier, out IReadOnlyCollection<Guid> backlinkIds)
    {
        if (_backlinks.TryGetValue(nodeIdentifier, out var values))
        {
            backlinkIds = values;
            return true;
        }

        backlinkIds = Array.Empty<Guid>();
        return false;
    }

    public bool TryGetNode(Guid nodeIdentifier, out Node node)
    {
        return _nodes.TryGetValue(nodeIdentifier, out node!);
    }

    public bool TryGetNodeFile(Guid nodeIdentifier, out string mapFilePath)
    {
        return _nodeFiles.TryGetValue(nodeIdentifier, out mapFilePath!);
    }

    private void IndexMap(MindMap map, string mapFilePath)
    {
        IndexNode(map.RootNode, mapFilePath);
    }

    private void IndexNode(Node node, string mapFilePath)
    {
        if (node.UniqueIdentifier.HasValue && node.UniqueIdentifier.Value != Guid.Empty)
        {
            var nodeIdentifier = node.UniqueIdentifier.Value;
            _nodes[nodeIdentifier] = node;
            _nodeFiles[nodeIdentifier] = mapFilePath;

            foreach (var link in node.Links.Values)
            {
                if (!_backlinks.TryGetValue(link.id, out var backlinkIdentifiers))
                {
                    backlinkIdentifiers = new HashSet<Guid>();
                    _backlinks[link.id] = backlinkIdentifiers;
                }

                backlinkIdentifiers.Add(nodeIdentifier);
            }
        }

        foreach (var childNode in node.Children)
        {
            IndexNode(childNode, mapFilePath);
        }
    }
}
