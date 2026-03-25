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

        foreach (var run in InlineFormatParser.Parse(normalizedInput))
        {
            var encodedText = WebUtility.HtmlEncode(run.Text);
            if (!run.ForegroundColor.HasValue)
            {
                htmlBuilder.Append(encodedText);
                continue;
            }

            var colorName = ColorfulConsole.ConsoleColorNames[run.ForegroundColor.Value];
            htmlBuilder.Append($"<span class=\"color-{colorName}\">{encodedText}</span>");
        }

        return htmlBuilder.ToString();
    }

    public static string ToPlainText(string input) =>
        PlainTextInlineFormatter.ToPlainText(NodeExportHelpers.NormalizeNodeName(input));
}
