#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Application;

namespace Systems.Sanity.Focus.DomainServices;

internal static class MapsSearchService
{
    public static IReadOnlyList<NodeSearchResult> Search(IMapRepository mapRepository, string query, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<NodeSearchResult>();

        var usedIdentifiers = new HashSet<Guid>();

        return mapRepository.GetAll()
            .SelectMany(file =>
            {
                var map = mapRepository.OpenMap(file.FullName, usedIdentifiers);
                return MindMapSearchService.Search(map, query, file.FullName, int.MaxValue);
            })
            .OrderBy(result => result.Score)
            .ThenBy(result => result.Depth)
            .ThenBy(result => result.NodePath.Length)
            .Take(maxResults)
            .ToArray();
    }
}
