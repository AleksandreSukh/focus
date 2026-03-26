#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal sealed class LinkNavigationService
{
    private readonly ILinkIndex _linkIndex;

    public LinkNavigationService(ILinkIndex linkIndex)
    {
        _linkIndex = linkIndex;
    }

    public IReadOnlyList<NodeSearchResult> GetOutgoingLinks(Node node)
    {
        return node.Links.Values
            .Select(link => CreateSearchResult(link.id, link.relationType.ToDisplayString()))
            .OfType<NodeSearchResult>()
            .OrderBy(result => result.MapName)
            .ThenBy(result => result.NodePath)
            .ToArray();
    }

    public IReadOnlyList<NodeSearchResult> GetBacklinks(Node node)
    {
        if (!node.UniqueIdentifier.HasValue ||
            !_linkIndex.TryGetBacklinkIds(node.UniqueIdentifier.Value, out var backlinkIdentifiers) ||
            backlinkIdentifiers.Count == 0)
        {
            return Array.Empty<NodeSearchResult>();
        }

        return backlinkIdentifiers
            .Select(backlinkIdentifier => CreateBacklinkSearchResult(backlinkIdentifier, node.UniqueIdentifier.Value))
            .OfType<NodeSearchResult>()
            .OrderBy(result => result.MapName)
            .ThenBy(result => result.NodePath)
            .ToArray();
    }

    private NodeSearchResult? CreateBacklinkSearchResult(Guid sourceNodeIdentifier, Guid targetNodeIdentifier)
    {
        if (!_linkIndex.TryGetNode(sourceNodeIdentifier, out var sourceNode))
            return null;

        sourceNode.Links.TryGetValue(targetNodeIdentifier, out var backlink);
        var relationLabel = backlink == null
            ? "backlink"
            : $"backlink: {backlink.relationType.ToDisplayString()}";

        return CreateSearchResult(sourceNodeIdentifier, relationLabel);
    }

    private NodeSearchResult? CreateSearchResult(Guid nodeIdentifier, string? contextLabel = null)
    {
        if (!_linkIndex.TryGetNode(nodeIdentifier, out var linkedNode))
            return null;

        _linkIndex.TryGetNodeFile(nodeIdentifier, out var mapFilePath);
        mapFilePath ??= string.Empty;

        var mapName = string.IsNullOrWhiteSpace(mapFilePath)
            ? linkedNode.Name
            : Path.GetFileNameWithoutExtension(mapFilePath);

        return new NodeSearchResult(
            nodeIdentifier,
            linkedNode.Name,
            NodeDisplayHelper.BuildNodePath(linkedNode),
            mapFilePath,
            mapName,
            Score: 0,
            Depth: NodeDisplayHelper.GetDepth(linkedNode),
            ContextLabel: contextLabel);
    }
}
