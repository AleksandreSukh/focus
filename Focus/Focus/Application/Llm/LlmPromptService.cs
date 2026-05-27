#nullable enable

using System;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Application.Llm;

internal static class LlmPromptService
{
    public const string PromptPrefix = "@ai ";
    public const string ContextMode = "subtree-links";
    public const string DefaultAgentName = "Codex";

    public static bool IsPromptNode(Node? node) =>
        node != null &&
        node.NodeType != NodeType.IdeaBagItem &&
        node.TaskState.IsOpenTask() &&
        !string.IsNullOrWhiteSpace(ExtractPromptText(node.Name));

    public static string ExtractPromptText(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.StartsWith(PromptPrefix, StringComparison.OrdinalIgnoreCase)
            ? text[PromptPrefix.Length..].Trim()
            : string.Empty;
    }

    public static string GetPromptText(Node node)
    {
        var promptText = ExtractPromptText(node.Name);
        return string.IsNullOrWhiteSpace(promptText)
            ? node.Name.Trim()
            : promptText;
    }

    public static string BuildPromptNodeName(string prompt)
    {
        var normalizedPrompt = ExtractPromptText(prompt);
        if (string.IsNullOrWhiteSpace(normalizedPrompt))
            normalizedPrompt = (prompt ?? string.Empty).Trim();

        return $"{PromptPrefix}{normalizedPrompt}";
    }

    public static string GetTaskStateLabel(TaskState taskState) =>
        taskState switch
        {
            TaskState.Todo => "todo",
            TaskState.Doing => "doing",
            TaskState.Done => "done",
            _ => "none"
        };
}
