using System.Net;
using System.Text;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.DomainServices;

internal static class HtmlInlineFormatter
{
    public static string ToHtml(string input)
    {
        var normalizedInput = NodeExportHelpers.NormalizeNodeName(input);
        var htmlBuilder = new StringBuilder(normalizedInput.Length + 32);
        var pendingText = new StringBuilder();
        string activeColorClass = null;

        void FlushPendingText()
        {
            if (pendingText.Length == 0)
                return;

            htmlBuilder.Append(WebUtility.HtmlEncode(pendingText.ToString()));
            pendingText.Clear();
        }

        void CloseActiveSpan()
        {
            if (activeColorClass == null)
                return;

            htmlBuilder.Append("</span>");
            activeColorClass = null;
        }

        for (var index = 0; index < normalizedInput.Length; index++)
        {
            var currentCharacter = normalizedInput[index];
            if (currentCharacter != ColorfulConsole.CommandStartBracket)
            {
                pendingText.Append(currentCharacter);
                continue;
            }

            var commandEndIndex = normalizedInput.IndexOf(ColorfulConsole.CommandEndBracket, index + 1);
            if (commandEndIndex < 0)
            {
                pendingText.Append(currentCharacter);
                continue;
            }

            var command = normalizedInput[(index + 1)..commandEndIndex];
            if (command == ColorfulConsole.ColorCommandTerminationTag)
            {
                FlushPendingText();
                CloseActiveSpan();
                index = commandEndIndex;
                continue;
            }

            if (ColorfulConsole.Colors.TryGetValue(command.ToLowerInvariant(), out _))
            {
                FlushPendingText();
                CloseActiveSpan();
                activeColorClass = $"color-{command.ToLowerInvariant()}";
                htmlBuilder.Append($"<span class=\"{activeColorClass}\">");
                index = commandEndIndex;
                continue;
            }

            pendingText.Append(normalizedInput[index..(commandEndIndex + 1)]);
            index = commandEndIndex;
        }

        FlushPendingText();
        CloseActiveSpan();
        return htmlBuilder.ToString();
    }

    public static string ToPlainText(string input)
    {
        var normalizedInput = NodeExportHelpers.NormalizeNodeName(input);
        var plainTextBuilder = new StringBuilder(normalizedInput.Length);

        for (var index = 0; index < normalizedInput.Length; index++)
        {
            var currentCharacter = normalizedInput[index];
            if (currentCharacter != ColorfulConsole.CommandStartBracket)
            {
                plainTextBuilder.Append(currentCharacter);
                continue;
            }

            var commandEndIndex = normalizedInput.IndexOf(ColorfulConsole.CommandEndBracket, index + 1);
            if (commandEndIndex < 0)
            {
                plainTextBuilder.Append(currentCharacter);
                continue;
            }

            var command = normalizedInput[(index + 1)..commandEndIndex];
            var isKnownColorCommand = command == ColorfulConsole.ColorCommandTerminationTag
                || ColorfulConsole.Colors.ContainsKey(command.ToLowerInvariant());

            if (!isKnownColorCommand)
            {
                plainTextBuilder.Append(normalizedInput[index..(commandEndIndex + 1)]);
            }

            index = commandEndIndex;
        }

        return plainTextBuilder.ToString();
    }
}
