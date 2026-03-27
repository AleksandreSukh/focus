#nullable enable

using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Application;

internal static class TaskCommandHelper
{
    private static readonly TaskListFilter[] ExplicitFilters =
    {
        TaskListFilter.Todo,
        TaskListFilter.Doing,
        TaskListFilter.Done,
        TaskListFilter.All
    };

    public static string BuildEmptyTasksMessage(TaskListFilter filter, bool acrossAllMaps)
    {
        var scope = acrossAllMaps ? "across all maps" : "in current map";
        return $"No {GetTaskDescription(filter)} {scope}";
    }

    public static IEnumerable<string> GetTaskListSuggestions(params string[] commands)
    {
        return commands
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .SelectMany(command => new[] { command }.Concat(
                ExplicitFilters.Select(filter => $"{command} {filter.ToCommandValue()}")))
            .Distinct();
    }

    public static string GetTasksTitle(TaskListFilter filter, bool acrossAllMaps)
    {
        var scope = acrossAllMaps ? "across all maps" : "in current map";
        return $"{GetTaskDescription(filter)} {scope}";
    }

    public static bool TryParseFilter(string? input, out TaskListFilter filter, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            filter = TaskListFilter.Open;
            errorMessage = null;
            return true;
        }

        switch (input.Trim().ToCommandLanguage())
        {
            case "todo":
                filter = TaskListFilter.Todo;
                errorMessage = null;
                return true;
            case "doing":
                filter = TaskListFilter.Doing;
                errorMessage = null;
                return true;
            case "done":
                filter = TaskListFilter.Done;
                errorMessage = null;
                return true;
            case "all":
                filter = TaskListFilter.All;
                errorMessage = null;
                return true;
            default:
                filter = TaskListFilter.Open;
                errorMessage = $"Unsupported task filter \"{input}\". Use todo, doing, done, or all.";
                return false;
        }
    }

    private static string GetTaskDescription(TaskListFilter filter) =>
        filter == TaskListFilter.All
            ? "tasks"
            : $"{filter.ToDisplayLabel()} tasks";
}
