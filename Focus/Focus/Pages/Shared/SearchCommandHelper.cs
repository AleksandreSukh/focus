using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Pages.Shared;

internal static class SearchCommandHelper
{
    public static SearchCommandResult Run(
        string query,
        Func<string, IReadOnlyList<NodeSearchResult>> search,
        bool includeMapName)
    {
        if (string.IsNullOrWhiteSpace(query))
            return SearchCommandResult.Error("Search query is empty");

        var trimmedQuery = query.Trim();
        var searchResults = search(trimmedQuery);
        if (!searchResults.Any())
            return SearchCommandResult.Error($"No matches for \"{trimmedQuery}\"");

        var selectedResult = new SearchResultsPage(
            searchResults,
            $"Search results for \"{trimmedQuery}\"",
            includeMapName)
            .SelectResult();

        return SearchCommandResult.Success(selectedResult);
    }
}

internal sealed record SearchCommandResult(
    NodeSearchResult SelectedResult,
    string ErrorMessage)
{
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public static SearchCommandResult Error(string errorMessage) =>
        new(null, errorMessage);

    public static SearchCommandResult Success(NodeSearchResult selectedResult) =>
        new(selectedResult, null);
}
