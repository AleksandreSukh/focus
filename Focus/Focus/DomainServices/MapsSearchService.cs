using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class MapsSearchService
{
    public static IReadOnlyList<NodeSearchResult> Search(MapsStorage mapsStorage, string query, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<NodeSearchResult>();

        var usedIdentifiers = new HashSet<Guid>();

        return mapsStorage.GetAll()
            .SelectMany(file =>
            {
                var map = MapFile.OpenFile(file.FullName, usedIdentifiers);
                return MindMapSearchService.Search(map, query, file.FullName, int.MaxValue);
            })
            .OrderBy(result => result.Score)
            .ThenBy(result => result.Depth)
            .ThenBy(result => result.NodePath.Length)
            .Take(maxResults)
            .ToArray();
    }
}
