#nullable enable

using System;
using System.Collections.Generic;

namespace Systems.Sanity.Focus.Application.Console;

internal static class MultilineInputCollector
{
    private const string ContinuationPrompt = "> ";

    public static string Read(Func<string, string> readLine, string prompt, string defaultInput = "") =>
        Read((linePrompt, _) => readLine(linePrompt), prompt, defaultInput);

    public static string Read(
        Func<string, string, string> readLine,
        string prompt,
        string defaultInput = "",
        string initialText = "")
    {
        var lines = new List<string>();
        var initialLines = SplitInitialText(initialText);
        var initialLineIndex = 0;
        var currentPrompt = prompt;

        while (true)
        {
            var hasSeededLine = initialLineIndex < initialLines.Count;
            var lineInitialText = hasSeededLine
                ? initialLines[initialLineIndex]
                : string.Empty;
            var line = readLine(currentPrompt, lineInitialText);
            initialLineIndex++;
            currentPrompt = ContinuationPrompt;

            if (!hasSeededLine && line.Length == 0 && lines.Count > 0 && lines[^1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
                break;
            }

            lines.Add(line);
        }

        var text = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(defaultInput)
            ? defaultInput
            : text;
    }

    private static IReadOnlyList<string> SplitInitialText(string initialText)
    {
        if (string.IsNullOrEmpty(initialText))
            return Array.Empty<string>();

        return initialText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }
}
