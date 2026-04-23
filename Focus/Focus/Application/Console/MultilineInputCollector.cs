#nullable enable

using System;
using System.Collections.Generic;

namespace Systems.Sanity.Focus.Application.Console;

internal static class MultilineInputCollector
{
    private const string ContinuationPrompt = "> ";

    public static string Read(Func<string, string> readLine, string prompt, string defaultInput = "")
    {
        var lines = new List<string>();
        var currentPrompt = prompt;

        while (true)
        {
            var line = readLine(currentPrompt);
            currentPrompt = ContinuationPrompt;

            if (line.Length == 0 && lines.Count > 0 && lines[^1].Length == 0)
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
}
