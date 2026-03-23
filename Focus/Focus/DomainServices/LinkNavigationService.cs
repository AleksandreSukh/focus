using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class LinkNavigationService
{
    public static IReadOnlyList<NodeSearchResult> GetOutgoingLinks(Node node)
    {
        return node.Links.Values
            .Select(link => CreateSearchResult(link.id, link.relationType.ToDisplayString()))
            .Where(result => result != null)
            .OrderBy(result => result.MapName)
            .ThenBy(result => result.NodePath)
            .ToArray();
    }

    public static IReadOnlyList<NodeSearchResult> GetBacklinks(Node node)
    {
        if (!node.UniqueIdentifier.HasValue ||
            !GlobalLinkDitionary.Backlinks.TryGetValue(node.UniqueIdentifier.Value, out var backlinkIdentifiers))
        {
            return Array.Empty<NodeSearchResult>();
        }

        return backlinkIdentifiers
            .Select(backlinkIdentifier => CreateBacklinkSearchResult(backlinkIdentifier, node.UniqueIdentifier.Value))
            .Where(result => result != null)
            .OrderBy(result => result.MapName)
            .ThenBy(result => result.NodePath)
            .ToArray();
    }

    private static NodeSearchResult CreateBacklinkSearchResult(Guid sourceNodeIdentifier, Guid targetNodeIdentifier)
    {
        if (!GlobalLinkDitionary.Nodes.TryGetValue(sourceNodeIdentifier, out var sourceNode))
            return null;

        sourceNode.Links.TryGetValue(targetNodeIdentifier, out var backlink);
        var relationLabel = backlink == null
            ? "backlink"
            : $"backlink: {backlink.relationType.ToDisplayString()}";

        return CreateSearchResult(sourceNodeIdentifier, relationLabel);
    }

    private static NodeSearchResult CreateSearchResult(Guid nodeIdentifier, string contextLabel = null)
    {
        if (!GlobalLinkDitionary.Nodes.TryGetValue(nodeIdentifier, out var linkedNode))
            return null;

        GlobalLinkDitionary.NodeFiles.TryGetValue(nodeIdentifier, out var mapFilePath);
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
