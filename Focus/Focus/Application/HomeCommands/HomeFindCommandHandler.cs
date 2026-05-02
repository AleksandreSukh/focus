#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;

namespace Systems.Sanity.Focus.Application.HomeCommands;

internal sealed class HomeFindCommandHandler : IHomeCommandFeatureHandler
{
    public IReadOnlyCollection<HomeCommandId> CommandIds { get; } = new[]
    {
        HomeCommandId.Search,
        HomeCommandId.ListTasks
    };

    public HomeWorkflowResult Execute(
        HomeCommandContext context,
        HomeCommandId commandId,
        ConsoleInput input,
        IReadOnlyDictionary<int, FileInfo> fileSelection) =>
        commandId switch
        {
            HomeCommandId.Search => HandleSearch(context, input),
            HomeCommandId.ListTasks => HandleTasks(context, input),
            _ => HomeWorkflowResult.Error($"Unsupported home command \"{commandId}\".")
        };

    private static HomeWorkflowResult HandleSearch(HomeCommandContext context, ConsoleInput input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input.Parameters))
                return HomeWorkflowResult.Error("Search query is empty");

            var trimmedQuery = input.Parameters.Trim();
            var searchResults = MapsSearchService.Search(context.AppContext.MapRepository, trimmedQuery);
            if (!searchResults.Any())
                return HomeWorkflowResult.Error($"No matches for \"{trimmedQuery}\"");

            var selectedResult = context.AppContext.WorkflowInteractions.SelectSearchResult(
                searchResults,
                $"Search results for \"{trimmedQuery}\"",
                new SearchResultDisplayOptions(
                    includeMapName: true,
                    colorizeAncestorPath: true,
                    trimmedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

            if (selectedResult != null)
            {
                context.AppContext.Navigator.OpenEditMap(
                    selectedResult.MapFilePath,
                    selectedResult.NodeId);
            }

            return HomeWorkflowResult.Continue;
        }
        catch (MapConflictAutoResolveException ex)
        {
            return HomeWorkflowResult.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return HomeWorkflowResult.Error(ExceptionDiagnostics.LogException(ex, "searching maps"));
        }
    }

    private static HomeWorkflowResult HandleTasks(HomeCommandContext context, ConsoleInput input)
    {
        try
        {
            if (!TaskCommandHelper.TryParseFilter(input.Parameters, out var filter, out var errorMessage))
                return HomeWorkflowResult.Error(errorMessage!);

            var tasks = TaskQueryService.GetTasks(context.AppContext.MapRepository, filter);
            if (!tasks.Any())
                return HomeWorkflowResult.Error(TaskCommandHelper.BuildEmptyTasksMessage(filter, acrossAllMaps: true));

            var selectedResult = context.AppContext.WorkflowInteractions.SelectSearchResult(
                tasks,
                TaskCommandHelper.GetTasksTitle(filter, acrossAllMaps: true),
                new SearchResultDisplayOptions(
                    includeMapName: true,
                    colorizeAncestorPath: true,
                    highlightTerms: Array.Empty<string>()));

            if (selectedResult != null)
            {
                context.AppContext.Navigator.OpenEditMap(
                    selectedResult.MapFilePath,
                    selectedResult.NodeId);
            }

            return HomeWorkflowResult.Continue;
        }
        catch (MapConflictAutoResolveException ex)
        {
            return HomeWorkflowResult.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return HomeWorkflowResult.Error(ExceptionDiagnostics.LogException(ex, "listing tasks"));
        }
    }
}
