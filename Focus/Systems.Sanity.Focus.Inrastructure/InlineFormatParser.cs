using System.Text;

namespace Systems.Sanity.Focus.Infrastructure;

internal static class InlineFormatParser
{
    public static IReadOnlyList<InlineFormatRun> Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
            return Array.Empty<InlineFormatRun>();

        var runs = new List<InlineFormatRun>();
        var pendingText = new StringBuilder();
        ConsoleColor? activeColor = null;

        void FlushPendingText()
        {
            if (pendingText.Length == 0)
                return;

            var text = pendingText.ToString();
            if (runs.Count > 0 && runs[^1].ForegroundColor == activeColor)
            {
                runs[^1] = runs[^1] with
                {
                    Text = runs[^1].Text + text
                };
            }
            else
            {
                runs.Add(new InlineFormatRun(text, activeColor));
            }

            pendingText.Clear();
        }

        for (var index = 0; index < input.Length; index++)
        {
            var currentCharacter = input[index];
            if (currentCharacter != ColorfulConsole.CommandStartBracket)
            {
                pendingText.Append(currentCharacter);
                continue;
            }

            var commandEndIndex = input.IndexOf(ColorfulConsole.CommandEndBracket, index + 1);
            if (commandEndIndex < 0)
            {
                pendingText.Append(currentCharacter);
                continue;
            }

            var command = input[(index + 1)..commandEndIndex];
            if (command == ColorfulConsole.ColorCommandTerminationTag)
            {
                FlushPendingText();
                activeColor = null;
                index = commandEndIndex;
                continue;
            }

            if (ColorfulConsole.Colors.TryGetValue(command.ToLowerInvariant(), out var color))
            {
                FlushPendingText();
                activeColor = color;
                index = commandEndIndex;
                continue;
            }

            pendingText.Append(input[index..(commandEndIndex + 1)]);
            index = commandEndIndex;
        }

        FlushPendingText();
        return runs;
    }
}
