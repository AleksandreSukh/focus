#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Pages.Shared;

internal sealed record SearchResultDisplayOptions
{
    public SearchResultDisplayOptions(
        bool includeMapName,
        bool colorizeAncestorPath,
        IReadOnlyList<string>? highlightTerms = null)
    {
        IncludeMapName = includeMapName;
        ColorizeAncestorPath = colorizeAncestorPath;
        HighlightTerms = highlightTerms ?? Array.Empty<string>();
    }

    public bool IncludeMapName { get; }

    public bool ColorizeAncestorPath { get; }

    public IReadOnlyList<string> HighlightTerms { get; }
}

internal static class SearchResultDisplayFormatter
{
    private static readonly ConsoleColor MatchHighlightColor = ConsoleColor.Cyan;

    public static string Format(NodeSearchResult result, SearchResultDisplayOptions options)
    {
        var builder = new StringBuilder();

        if (options.IncludeMapName)
        {
            builder.Append(result.MapName);
            builder.Append(": ");
        }

        var taskMarker = result.TaskState.ToDisplayMarker();
        if (!string.IsNullOrWhiteSpace(taskMarker))
        {
            builder.Append(taskMarker);
            builder.Append(' ');
        }

        AppendNodePath(builder, result, options);

        if (!string.IsNullOrWhiteSpace(result.ContextLabel))
        {
            builder.Append(" [");
            builder.Append(result.ContextLabel);
            builder.Append(']');
        }

        return builder.ToString();
    }

    private static void AppendNodePath(StringBuilder builder, NodeSearchResult result, SearchResultDisplayOptions options)
    {
        var nodePathSegments = result.NodePathSegments.Count > 0
            ? result.NodePathSegments
            : GetFallbackNodePathSegments(result);
        var highlightTerms = NormalizeHighlightTerms(options.HighlightTerms);

        for (var index = 0; index < nodePathSegments.Count; index++)
        {
            var isAncestorSegment = index < nodePathSegments.Count - 1;
            var baseColor = options.ColorizeAncestorPath && isAncestorSegment
                ? ConsoleColor.Yellow
                : (ConsoleColor?)null;

            AppendHighlightedText(builder, nodePathSegments[index], baseColor, highlightTerms);

            if (!isAncestorSegment)
                continue;

            AppendWithColor(
                builder,
                " > ",
                options.ColorizeAncestorPath ? ConsoleColor.DarkYellow : null);
        }
    }

    private static IReadOnlyList<string> GetFallbackNodePathSegments(NodeSearchResult result) =>
        string.IsNullOrWhiteSpace(result.NodePath)
            ? new[] { result.NodeName }
            : result.NodePath.Split(" > ", StringSplitOptions.None);

    private static string[] NormalizeHighlightTerms(IReadOnlyList<string> highlightTerms) =>
        highlightTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim())
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .OrderByDescending(term => term.Length)
            .ToArray();

    private static void AppendHighlightedText(
        StringBuilder builder,
        string text,
        ConsoleColor? baseColor,
        IReadOnlyList<string> highlightTerms)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (highlightTerms.Count == 0)
        {
            AppendWithColor(builder, text, baseColor);
            return;
        }

        var currentIndex = 0;
        while (currentIndex < text.Length)
        {
            var matchLength = GetMatchLength(text, currentIndex, highlightTerms);
            if (matchLength > 0)
            {
                AppendWithColor(builder, text.Substring(currentIndex, matchLength), MatchHighlightColor);
                currentIndex += matchLength;
                continue;
            }

            var nextIndex = currentIndex + 1;
            while (nextIndex < text.Length && GetMatchLength(text, nextIndex, highlightTerms) == 0)
            {
                nextIndex++;
            }

            AppendWithColor(builder, text.Substring(currentIndex, nextIndex - currentIndex), baseColor);
            currentIndex = nextIndex;
        }
    }

    private static int GetMatchLength(string text, int startIndex, IReadOnlyList<string> highlightTerms)
    {
        foreach (var term in highlightTerms)
        {
            if (startIndex + term.Length > text.Length)
                continue;

            if (text.AsSpan(startIndex, term.Length).Equals(term, StringComparison.InvariantCultureIgnoreCase))
                return term.Length;
        }

        return 0;
    }

    private static void AppendWithColor(StringBuilder builder, string text, ConsoleColor? color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (!color.HasValue)
        {
            builder.Append(text);
            return;
        }

        builder.Append('[');
        builder.Append(ColorfulConsole.ConsoleColorNames[color.Value]);
        builder.Append(']');
        builder.Append(text);
        builder.Append("[!]");
    }
}
