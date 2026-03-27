#nullable enable

namespace Systems.Sanity.Focus.Domain;

public enum TaskState
{
    None = 0,
    Todo = 1,
    Doing = 2,
    Done = 3
}

internal static class TaskStateExtensions
{
    public static string ToDisplayMarker(this TaskState taskState) =>
        taskState switch
        {
            TaskState.None => string.Empty,
            TaskState.Todo => "[ ]",
            TaskState.Doing => "[~]",
            TaskState.Done => "[x]",
            _ => string.Empty
        };

    public static string WithDisplayMarker(this TaskState taskState, string text)
    {
        var marker = taskState.ToDisplayMarker();
        if (string.IsNullOrWhiteSpace(marker))
            return text;

        return string.IsNullOrWhiteSpace(text)
            ? marker
            : $"{marker} {text}";
    }

    public static bool IsOpenTask(this TaskState taskState) =>
        taskState == TaskState.Todo || taskState == TaskState.Doing;

    public static bool IsTask(this TaskState taskState) => taskState != TaskState.None;

    public static int ToSortPriority(this TaskState taskState) =>
        taskState switch
        {
            TaskState.Doing => 0,
            TaskState.Todo => 1,
            TaskState.Done => 2,
            _ => 3
        };

    public static TaskState Toggle(this TaskState taskState) =>
        taskState switch
        {
            TaskState.None => TaskState.Todo,
            TaskState.Todo => TaskState.Done,
            TaskState.Doing => TaskState.Done,
            TaskState.Done => TaskState.Todo,
            _ => TaskState.Todo
        };
}
