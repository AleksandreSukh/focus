using System.Text;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.DomainServices;

internal static class PlainTextInlineFormatter
{
    public static string ToPlainText(string input)
    {
        input ??= string.Empty;

        var plainTextBuilder = new StringBuilder(input.Length);
        foreach (var run in InlineFormatParser.Parse(input))
        {
            plainTextBuilder.Append(run.Text);
        }

        return plainTextBuilder.ToString();
    }
}
