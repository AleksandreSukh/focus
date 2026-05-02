#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditSearchCommandHandler : IEditCommandFeatureHandler
{
    public IReadOnlyCollection<EditCommandId> CommandIds { get; } = new[]
    {
        EditCommandId.Search
    };

    public CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters) =>
        commandId switch
        {
            EditCommandId.Search => ProcessSearch(context, parameters),
            _ => CommandExecutionResult.Error($"Unsupported command \"{commandId}\"")
        };

    private static CommandExecutionResult ProcessSearch(EditCommandContext context, string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return CommandExecutionResult.Error("Search query is empty");

        var trimmedQuery = parameters.Trim();
        var searchResults = MindMapSearchService.Search(context.Map, trimmedQuery, context.FilePath);
        if (!searchResults.Any())
            return CommandExecutionResult.Error($"No matches for \"{trimmedQuery}\"");

        var selectedResult = context.AppContext.WorkflowInteractions.SelectSearchResult(
            searchResults,
            $"Search results for \"{trimmedQuery}\"",
            new SearchResultDisplayOptions(
                includeMapName: false,
                colorizeAncestorPath: true,
                trimmedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

        if (selectedResult == null)
            return CommandExecutionResult.Success();

        return context.Map.ChangeCurrentNodeById(selectedResult.NodeId)
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.Error("Couldn't open selected result");
    }
}
