using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class NodeDisplayHelper
{
    public static string BuildDisplayName(Node node) =>
        PrefixTaskMarker(GetSingleLinePreview(node.Name), node.TaskState);

    public static string BuildNodePath(Node node) =>
        string.Join(" > ", BuildNodePathSegments(node));

    public static IReadOnlyList<string> BuildNodePathSegments(Node node)
    {
        var pathSegments = new Stack<string>();
        var currentNode = node;
        while (currentNode != null)
        {
            var displaySegment = GetSingleLinePreview(currentNode.Name);
            if (!string.IsNullOrWhiteSpace(displaySegment))
            {
                pathSegments.Push(displaySegment);
            }

            currentNode = currentNode.GetParent();
        }

        return pathSegments.ToArray();
    }

    public static int GetDepth(Node node)
    {
        var depth = 0;
        var currentNode = node.GetParent();
        while (currentNode != null)
        {
            depth++;
            currentNode = currentNode.GetParent();
        }

        return depth;
    }

    public static string PrefixTaskMarker(string text, TaskState taskState) =>
        taskState.WithDisplayMarker(text);

    public static string GetSingleLinePreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lines = NormalizeLineEndings(text).Split('\n');
        var firstNonEmptyLine = lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        return firstNonEmptyLine?.Trim() ?? string.Empty;
    }

    public static string GetContentPeek(string? text, int maxLength = 32)
    {
        var preview = GetSingleLinePreview(text);
        if (preview.Length <= maxLength)
            return preview;

        return $"{preview[..maxLength]}...";
    }

    public static string[] GetMultilineLines(string? text) =>
        NormalizeLineEndings(text).Split('\n');

    private static string NormalizeLineEndings(string? text) =>
        (text ?? string.Empty).ReplaceLineEndings("\n");
}
