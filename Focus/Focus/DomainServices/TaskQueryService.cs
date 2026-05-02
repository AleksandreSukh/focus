#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class TaskQueryService
{
    public static IReadOnlyList<NodeSearchResult> GetTasks(
        MindMap map,
        string mapFilePath,
        TaskListFilter filter = TaskListFilter.Open,
        int maxResults = 200)
    {
        var mapName = Path.GetFileNameWithoutExtension(mapFilePath);

        return Traverse(map.RootNode)
            .Where(node => IsMatchingTask(node, filter))
            .Select(node => CreateSearchResult(node, mapFilePath, mapName))
            .OrderBy(result => result.TaskState.ToSortPriority())
            .ThenBy(result => result.Depth)
            .ThenBy(result => result.NodePath, StringComparer.InvariantCultureIgnoreCase)
            .Take(maxResults)
            .ToArray();
    }

    public static IReadOnlyList<NodeSearchResult> GetTasks(
        IMapRepository mapRepository,
        TaskListFilter filter = TaskListFilter.Open,
        int maxResults = 200)
    {
        var usedIdentifiers = new HashSet<Guid>();

        return mapRepository.GetAll()
            .SelectMany(file =>
            {
                var map = mapRepository.OpenMap(file.FullName, usedIdentifiers);
                return GetTasks(map, file.FullName, filter, int.MaxValue);
            })
            .OrderBy(result => result.TaskState.ToSortPriority())
            .ThenBy(result => result.MapName, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(result => result.NodePath, StringComparer.InvariantCultureIgnoreCase)
            .Take(maxResults)
            .ToArray();
    }

    private static NodeSearchResult CreateSearchResult(Node node, string mapFilePath, string mapName)
    {
        var nodePathSegments = NodeDisplayHelper.BuildNodePathSegments(node);

        return new NodeSearchResult(
            node.UniqueIdentifier!.Value,
            NodeDisplayHelper.GetSingleLinePreview(node.Name),
            string.Join(" > ", nodePathSegments),
            mapFilePath,
            mapName,
            Score: node.TaskState.ToSortPriority(),
            Depth: NodeDisplayHelper.GetDepth(node),
            TaskState: node.TaskState)
        {
            NodePathSegments = nodePathSegments
        };
    }

    private static bool IsMatchingTask(Node node, TaskListFilter filter)
    {
        if (node.NodeType == NodeType.IdeaBagItem ||
            node.GetParent() == null ||
            !node.UniqueIdentifier.HasValue ||
            node.UniqueIdentifier == Guid.Empty)
        {
            return false;
        }

        return filter.Includes(node.TaskState);
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
}
