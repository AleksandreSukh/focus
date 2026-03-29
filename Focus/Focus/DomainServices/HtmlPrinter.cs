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
    private const string LightBaseTextColor = "#1f2328";
    private const string DarkBaseTextColor = "#f5f7fa";
    private const string LightBackgroundColor = "#ffffff";
    private const string DarkBackgroundColor = "#000000";
    private const string DarkLinkColor = "#7dd3fc";

    public static void Print(Node node, StringBuilder sb, NodeExportOptions options = null)
    {
        options ??= new NodeExportOptions();

        var documentTitle = HtmlInlineFormatter.ToPlainText(NodeExportHelpers.FormatNodeName(node));

        AppendLine(sb, 0, "<!DOCTYPE html>");
        AppendLine(sb, 0, "<html lang=\"en\">");
        AppendLine(sb, 1, "<head>");
        AppendLine(sb, 2, "<meta charset=\"utf-8\" />");
        AppendLine(sb, 2, "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        AppendLine(sb, 2, $"<title>{WebUtility.HtmlEncode(documentTitle)}</title>");
        AppendStyles(sb, options);
        AppendLine(sb, 1, "</head>");
        AppendLine(sb, 1, "<body>");
        AppendLine(sb, 2, "<article class=\"mindmap-export\">");
        AppendLine(sb, 3, $"<h1>{HtmlInlineFormatter.ToHtml(NodeExportHelpers.FormatNodeName(node))}</h1>");

        var visibleChildren = NodeExportHelpers.GetVisibleChildren(node).ToArray();
        if (visibleChildren.Any())
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
        AppendLine(sb, indentationLevel + 1, HtmlInlineFormatter.ToHtml(NodeExportHelpers.FormatNodeName(node)));

        var visibleChildren = NodeExportHelpers.GetVisibleChildren(node).ToArray();
        if (visibleChildren.Any() && !(options.SkipCollapsedDescendants && node.IsCollapsed()))
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

    private static void AppendStyles(StringBuilder sb, NodeExportOptions options)
    {
        var useBlackBackground = options.UseBlackBackground;
        AppendLine(sb, 2, "<style>");
        AppendLine(sb, 3, useBlackBackground ? ":root { color-scheme: dark; }" : ":root { color-scheme: light; }");
        AppendLine(
            sb,
            3,
            $"body {{ margin: 0; padding: 2rem; font-family: Segoe UI, Arial, sans-serif; line-height: 1.6; color: {(useBlackBackground ? DarkBaseTextColor : LightBaseTextColor)}; background: {(useBlackBackground ? DarkBackgroundColor : LightBackgroundColor)}; }}");
        AppendLine(sb, 3, ".mindmap-export { max-width: 960px; margin: 0 auto; }");
        AppendLine(sb, 3, "h1 { margin-bottom: 1rem; font-size: 2rem; }");
        AppendLine(sb, 3, "ol, ul { padding-left: 1.5rem; }");
        AppendLine(sb, 3, "li { margin: 0.4rem 0; }");
        AppendLine(
            sb,
            3,
            useBlackBackground
                ? $".mindmap-export a, .mindmap-export a:visited {{ color: {DarkLinkColor}; text-decoration: underline; text-decoration-color: currentColor; }}"
                : ".mindmap-export a, .mindmap-export a:visited { color: inherit; text-decoration: underline; text-decoration-color: currentColor; }");

        foreach (var colorEntry in ColorfulConsole.Colors.OrderBy(entry => entry.Key))
        {
            AppendLine(
                sb,
                3,
                $".color-{colorEntry.Key} {{ color: {GetCssColor(colorEntry.Value, useBlackBackground)}; }}");
        }

        AppendLine(sb, 2, "</style>");
    }

    private static string GetCssColor(ConsoleColor color, bool useBlackBackground) =>
        useBlackBackground
            ? GetDarkThemeCssColor(color)
            : color switch
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
                _ => LightBaseTextColor
            };

    private static string GetDarkThemeCssColor(ConsoleColor color) =>
        color switch
        {
            ConsoleColor.Black => DarkBaseTextColor,
            ConsoleColor.DarkBlue => "#60a5fa",
            ConsoleColor.DarkGreen => "#4ade80",
            ConsoleColor.DarkCyan => "#22d3ee",
            ConsoleColor.DarkRed => "#f87171",
            ConsoleColor.DarkMagenta => "#e879f9",
            ConsoleColor.DarkYellow => "#facc15",
            ConsoleColor.Gray => "#d1d5db",
            ConsoleColor.DarkGray => "#9ca3af",
            ConsoleColor.Blue => "#93c5fd",
            ConsoleColor.Green => "#86efac",
            ConsoleColor.Cyan => "#67e8f9",
            ConsoleColor.Red => "#fca5a5",
            ConsoleColor.Magenta => "#f0abfc",
            ConsoleColor.Yellow => "#fde047",
            ConsoleColor.White => "#ffffff",
            _ => DarkBaseTextColor
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
