using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.DomainServices;

internal static class HtmlInlineFormatter
{
    private static readonly Regex UrlRegex = new(
        "https?://[^\\s<>\"']+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string ToHtml(string input)
    {
        var normalizedInput = NodeExportHelpers.NormalizeNodeName(input);
        var htmlBuilder = new StringBuilder(normalizedInput.Length + 32);

        foreach (var run in InlineFormatParser.Parse(normalizedInput))
        {
            if (!run.ForegroundColor.HasValue)
            {
                AppendRunContent(htmlBuilder, run.Text);
                continue;
            }

            var colorName = ColorfulConsole.ConsoleColorNames[run.ForegroundColor.Value];
            htmlBuilder.Append($"<span class=\"color-{colorName}\">");
            AppendRunContent(htmlBuilder, run.Text);
            htmlBuilder.Append("</span>");
        }

        return htmlBuilder.ToString();
    }

    public static string ToPlainText(string input) =>
        PlainTextInlineFormatter.ToPlainText(NodeExportHelpers.NormalizeNodeName(input));

    private static void AppendRunContent(StringBuilder htmlBuilder, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var currentIndex = 0;
        foreach (Match match in UrlRegex.Matches(text))
        {
            if (match.Index > currentIndex)
            {
                AppendEncodedText(
                    htmlBuilder,
                    text[currentIndex..match.Index]);
            }

            var matchedText = match.Value;
            var trimmedUrl = TrimTrailingUrlPunctuation(matchedText);
            if (IsSupportedHttpUrl(trimmedUrl))
            {
                AppendAnchor(htmlBuilder, trimmedUrl);

                if (trimmedUrl.Length < matchedText.Length)
                {
                    AppendEncodedText(
                        htmlBuilder,
                        matchedText[trimmedUrl.Length..]);
                }
            }
            else
            {
                AppendEncodedText(htmlBuilder, matchedText);
            }

            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < text.Length)
        {
            AppendEncodedText(htmlBuilder, text[currentIndex..]);
        }
    }

    private static void AppendAnchor(StringBuilder htmlBuilder, string url)
    {
        var encodedUrl = WebUtility.HtmlEncode(url);
        htmlBuilder.Append($"<a href=\"{encodedUrl}\" target=\"_blank\" rel=\"noopener noreferrer\">");
        htmlBuilder.Append(encodedUrl);
        htmlBuilder.Append("</a>");
    }

    private static void AppendEncodedText(StringBuilder htmlBuilder, string text) =>
        htmlBuilder.Append(WebUtility.HtmlEncode(text));

    private static bool IsSupportedHttpUrl(string candidate)
    {
        if (string.IsNullOrEmpty(candidate))
            return false;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private static string TrimTrailingUrlPunctuation(string candidate)
    {
        var endIndex = candidate.Length;
        while (endIndex > 0)
        {
            var trailingCharacter = candidate[endIndex - 1];
            if (ShouldTrimSimpleTrailingPunctuation(trailingCharacter) ||
                IsUnmatchedClosingDelimiter(candidate, endIndex, trailingCharacter))
            {
                endIndex--;
                continue;
            }

            break;
        }

        return candidate[..endIndex];
    }

    private static bool ShouldTrimSimpleTrailingPunctuation(char trailingCharacter) =>
        trailingCharacter is '.' or ',' or ';' or ':' or '!' or '?';

    private static bool IsUnmatchedClosingDelimiter(string candidate, int endIndex, char trailingCharacter)
    {
        return trailingCharacter switch
        {
            ')' => CountOccurrences(candidate, '(', endIndex) < CountOccurrences(candidate, ')', endIndex),
            ']' => CountOccurrences(candidate, '[', endIndex) < CountOccurrences(candidate, ']', endIndex),
            '}' => CountOccurrences(candidate, '{', endIndex) < CountOccurrences(candidate, '}', endIndex),
            _ => false
        };
    }

    private static int CountOccurrences(string input, char character, int length)
    {
        var count = 0;
        for (var index = 0; index < length; index++)
        {
            if (input[index] == character)
            {
                count++;
            }
        }

        return count;
    }
}
