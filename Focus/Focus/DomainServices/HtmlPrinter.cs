using System;
using System.Linq;
using System.Net;
using System.Text;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.DomainServices;

internal static class HtmlPrinter
{
    private const string Indentation = "  ";

    public static void Print(Node node, StringBuilder sb, NodeExportOptions options = null)
    {
        options ??= new NodeExportOptions();

        var documentTitle = HtmlInlineFormatter.ToPlainText(node.Name);

        AppendLine(sb, 0, "<!DOCTYPE html>");
        AppendLine(sb, 0, "<html lang=\"en\">");
        AppendLine(sb, 1, "<head>");
        AppendLine(sb, 2, "<meta charset=\"utf-8\" />");
        AppendLine(sb, 2, "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        AppendLine(sb, 2, $"<title>{WebUtility.HtmlEncode(documentTitle)}</title>");
        AppendStyles(sb);
        AppendLine(sb, 1, "</head>");
        AppendLine(sb, 1, "<body>");
        AppendLine(sb, 2, "<article class=\"mindmap-export\">");
        AppendLine(sb, 3, $"<h1>{HtmlInlineFormatter.ToHtml(node.Name)}</h1>");

        var visibleChildren = NodeExportHelpers.GetVisibleChildren(node).ToArray();
        if (visibleChildren.Any() && !(options.SkipCollapsedDescendants && node.Collapsed))
        {
            AppendLine(sb, 3, "<ol>");
            foreach (var childNode in visibleChildren)
            {
                PrintNode(childNode, level: 1, sb, options, indentationLevel: 4);
            }

            AppendLine(sb, 3, "</ol>");
        }

        AppendLine(sb, 2, "</article>");
        AppendLine(sb, 1, "</body>");
        AppendLine(sb, 0, "</html>");
    }

    private static void PrintNode(
        Node node,
        int level,
        StringBuilder sb,
        NodeExportOptions options,
        int indentationLevel)
    {
        AppendLine(sb, indentationLevel, "<li>");
        AppendLine(sb, indentationLevel + 1, HtmlInlineFormatter.ToHtml(node.Name));

        var visibleChildren = NodeExportHelpers.GetVisibleChildren(node).ToArray();
        if (visibleChildren.Any() && !(options.SkipCollapsedDescendants && node.Collapsed))
        {
            AppendLine(sb, indentationLevel + 1, "<ul>");
            foreach (var childNode in visibleChildren)
            {
                PrintNode(childNode, level + 1, sb, options, indentationLevel + 2);
            }

            AppendLine(sb, indentationLevel + 1, "</ul>");
        }

        AppendLine(sb, indentationLevel, "</li>");
    }

    private static void AppendStyles(StringBuilder sb)
    {
        AppendLine(sb, 2, "<style>");
        AppendLine(sb, 3, ":root { color-scheme: light; }");
        AppendLine(sb, 3, "body { margin: 0; padding: 2rem; font-family: Segoe UI, Arial, sans-serif; line-height: 1.6; color: #1f2328; background: #ffffff; }");
        AppendLine(sb, 3, ".mindmap-export { max-width: 960px; margin: 0 auto; }");
        AppendLine(sb, 3, "h1 { margin-bottom: 1rem; font-size: 2rem; }");
        AppendLine(sb, 3, "ol, ul { padding-left: 1.5rem; }");
        AppendLine(sb, 3, "li { margin: 0.4rem 0; }");

        foreach (var colorEntry in ColorfulConsole.Colors.OrderBy(entry => entry.Key))
        {
            AppendLine(
                sb,
                3,
                $".color-{colorEntry.Key} {{ color: {GetCssColor(colorEntry.Value)}; }}");
        }

        AppendLine(sb, 2, "</style>");
    }

    private static string GetCssColor(ConsoleColor color) =>
        color switch
        {
            ConsoleColor.Black => "#000000",
            ConsoleColor.DarkBlue => "#000080",
            ConsoleColor.DarkGreen => "#008000",
            ConsoleColor.DarkCyan => "#008080",
            ConsoleColor.DarkRed => "#800000",
            ConsoleColor.DarkMagenta => "#800080",
            ConsoleColor.DarkYellow => "#808000",
            ConsoleColor.Gray => "#c0c0c0",
            ConsoleColor.DarkGray => "#808080",
            ConsoleColor.Blue => "#0000ff",
            ConsoleColor.Green => "#008000",
            ConsoleColor.Cyan => "#00ffff",
            ConsoleColor.Red => "#ff0000",
            ConsoleColor.Magenta => "#ff00ff",
            ConsoleColor.Yellow => "#ffd700",
            ConsoleColor.White => "#ffffff",
            _ => "#1f2328"
        };

    private static void AppendLine(StringBuilder sb, int indentationLevel, string line)
    {
        for (var index = 0; index < indentationLevel; index++)
        {
            sb.Append(Indentation);
        }

        sb.AppendLine(line);
    }
}
