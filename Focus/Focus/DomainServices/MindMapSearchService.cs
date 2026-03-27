using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class MindMapSearchService
{
    public static IReadOnlyList<NodeSearchResult> Search(MindMap map, string query, string mapFilePath, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<NodeSearchResult>();

        var trimmedQuery = query.Trim();
        var searchTerms = trimmedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mapName = Path.GetFileNameWithoutExtension(mapFilePath);

        return Traverse(map.RootNode)
            .Select(node => CreateSearchResult(node, trimmedQuery, searchTerms, mapFilePath, mapName))
            .Where(result => result != null)
            .OrderBy(result => result.Score)
            .ThenBy(result => result.Depth)
            .ThenBy(result => result.NodePath.Length)
            .Take(maxResults)
            .ToArray();
    }

    private static NodeSearchResult CreateSearchResult(Node node, string fullQuery, string[] searchTerms, string mapFilePath, string mapName)
    {
        if (!node.UniqueIdentifier.HasValue || node.UniqueIdentifier == Guid.Empty)
            return null;

        var nodePathSegments = NodeDisplayHelper.BuildNodePathSegments(node);
        var nodePath = string.Join(" > ", nodePathSegments);
        var searchableText = $"{node.Name} {nodePath}";
        if (!searchTerms.All(term => searchableText.Contains(term, StringComparison.InvariantCultureIgnoreCase)))
            return null;

        return new NodeSearchResult(
            node.UniqueIdentifier.Value,
            node.Name,
            nodePath,
            mapFilePath,
            mapName,
            GetScore(node.Name, nodePath, fullQuery),
            NodeDisplayHelper.GetDepth(node),
            TaskState: node.TaskState)
        {
            NodePathSegments = nodePathSegments
        };
    }

    private static IEnumerable<Node> Traverse(Node node)
    {
        yield return node;

        foreach (var childNode in node.Children)
        {
            foreach (var nestedNode in Traverse(childNode))
            {
                yield return nestedNode;
            }
        }
    }

    private static int GetScore(string nodeName, string nodePath, string query)
    {
        if (nodeName.Equals(query, StringComparison.InvariantCultureIgnoreCase))
            return 0;

        if (nodeName.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
            return 1;

        if (nodeName.Contains(query, StringComparison.InvariantCultureIgnoreCase))
            return 2;

        if (nodePath.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
            return 3;

        return 4;
    }
}
