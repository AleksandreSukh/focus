using System;
using System.Text;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class MapExportService
{
    public static string Export(Node node, ExportFormat format, NodeExportOptions options = null)
    {
        options ??= new NodeExportOptions();

        var sb = new StringBuilder();

        switch (format)
        {
            case ExportFormat.Markdown:
                MarkdownPrinter.Print(node, sb, options);
                break;
            case ExportFormat.Html:
                HtmlPrinter.Print(node, sb, options);
                break;
            case ExportFormat.PlainText:
                PlainTextPrinter.Print(node, sb, options);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }

        return sb.ToString();
    }
}
