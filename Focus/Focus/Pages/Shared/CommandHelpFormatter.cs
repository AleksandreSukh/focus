#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application;

namespace Systems.Sanity.Focus.Pages.Shared;

internal sealed record CommandHelpGroup(
    string Label,
    IReadOnlyList<string> Entries);

internal static class CommandHelpFormatter
{
    public static string BuildGroupedLines(IEnumerable<CommandHelpGroup> groups) =>
        BuildGroupedLines(groups, AppConsole.Current.WindowWidth);

    public static string BuildWrappedOptionList(string label, IEnumerable<string> options) =>
        BuildWrappedOptionList(label, options, AppConsole.Current.WindowWidth);

    internal static string BuildGroupedLines(IEnumerable<CommandHelpGroup> groups, int maxWidth)
    {
        var builder = new StringBuilder();
        foreach (var group in groups)
        {
            AppendWrappedLine(builder, group.Label, group.Entries, maxWidth);
        }

        return builder.ToString();
    }

    internal static string BuildWrappedOptionList(string label, IEnumerable<string> options, int maxWidth)
    {
        var builder = new StringBuilder();
        AppendWrappedLine(builder, label, options, maxWidth);
        return builder.ToString();
    }

    private static void AppendWrappedLine(
        StringBuilder builder,
        string label,
        IEnumerable<string> entries,
        int maxWidth)
    {
        var entryArray = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToArray();
        if (!entryArray.Any())
            return;

        var prefix = $"{label}: ";
        var effectiveWidth = Math.Max(prefix.Length + 1, maxWidth);
        var continuationIndent = new string(' ', prefix.Length);
        var currentLineLength = prefix.Length;

        builder.Append(prefix);

        foreach (var entry in entryArray)
        {
            var isFirstEntryOnLine = currentLineLength == prefix.Length;
            var segmentLength = entry.Length + (isFirstEntryOnLine ? 0 : 2);
            if (!isFirstEntryOnLine && currentLineLength + segmentLength > effectiveWidth)
            {
                builder.AppendLine();
                builder.Append(continuationIndent);
                currentLineLength = prefix.Length;
                isFirstEntryOnLine = true;
            }

            if (!isFirstEntryOnLine)
            {
                builder.Append(", ");
                currentLineLength += 2;
            }

            builder.Append(FormatCommand(entry));
            currentLineLength += entry.Length;
        }

        builder.AppendLine();
    }

    private static string FormatCommand(string command) =>
        $"[{ConfigurationConstants.CommandColor}]{command}[!]";
}
