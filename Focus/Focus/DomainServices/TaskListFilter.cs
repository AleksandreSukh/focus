#nullable enable

using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal enum TaskListFilter
{
    Open,
    Todo,
    Doing,
    Done,
    All
}

internal static class TaskListFilterExtensions
{
    public static bool Includes(this TaskListFilter filter, TaskState taskState) =>
        filter switch
        {
            TaskListFilter.Open => taskState.IsOpenTask(),
            TaskListFilter.Todo => taskState == TaskState.Todo,
            TaskListFilter.Doing => taskState == TaskState.Doing,
            TaskListFilter.Done => taskState == TaskState.Done,
            TaskListFilter.All => taskState.IsTask(),
            _ => false
        };

    public static string ToCommandValue(this TaskListFilter filter) =>
        filter switch
        {
            TaskListFilter.Open => "open",
            TaskListFilter.Todo => "todo",
            TaskListFilter.Doing => "doing",
            TaskListFilter.Done => "done",
            TaskListFilter.All => "all",
            _ => "open"
        };

    public static string ToDisplayLabel(this TaskListFilter filter) =>
        filter switch
        {
            TaskListFilter.Open => "open",
            TaskListFilter.Todo => "todo",
            TaskListFilter.Doing => "doing",
            TaskListFilter.Done => "done",
            TaskListFilter.All => "all",
            _ => "open"
        };
}
