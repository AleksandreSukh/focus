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
        var ancestorHidesDone = NodeBranchVisibility.HasHideDoneAncestor(node);

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
        AppendBlockBody(node, sb, indentationLevel: 3);
        AppendAttachments(node, sb, options, indentationLevel: 3);

        var visibleChildren = NodeExportHelpers.GetVisibleChildren(node, ancestorHidesDone).ToArray();
        if (visibleChildren.Any())
        {
            AppendLine(sb, 3, "<ol>");
            var hideDoneStateForChildren = NodeBranchVisibility.HideDoneStateForChildren(node, ancestorHidesDone);
            foreach (var childNode in visibleChildren)
            {
                PrintNode(childNode, level: 1, sb, options, indentationLevel: 4, ancestorHidesDone: hideDoneStateForChildren);
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
        int indentationLevel,
        bool ancestorHidesDone)
    {
        AppendLine(sb, indentationLevel, "<li>");
        AppendLine(sb, indentationLevel + 1, "<div class=\"node-content\">");
        AppendLine(sb, indentationLevel + 2, $"<div class=\"node-text\">{HtmlInlineFormatter.ToHtml(NodeExportHelpers.FormatNodeName(node))}</div>");
        AppendBlockBody(node, sb, indentationLevel + 2);
        AppendAttachments(node, sb, options, indentationLevel + 2);

        var visibleChildren = NodeExportHelpers.GetVisibleChildren(node, ancestorHidesDone).ToArray();
        if (visibleChildren.Any() && !(options.SkipCollapsedDescendants && node.IsCollapsed()))
        {
            AppendLine(sb, indentationLevel + 2, "<ul>");
            var hideDoneStateForChildren = NodeBranchVisibility.HideDoneStateForChildren(node, ancestorHidesDone);
            foreach (var childNode in visibleChildren)
            {
                PrintNode(childNode, level + 1, sb, options, indentationLevel + 3, hideDoneStateForChildren);
            }

            AppendLine(sb, indentationLevel + 2, "</ul>");
        }

        AppendLine(sb, indentationLevel + 1, "</div>");
        AppendLine(sb, indentationLevel, "</li>");
    }

    private static void AppendAttachments(Node node, StringBuilder sb, NodeExportOptions options, int indentationLevel)
    {
        var attachments = NodeExportHelpers.GetAttachments(node, options);
        if (attachments.Count == 0)
            return;

        AppendLine(sb, indentationLevel, "<div class=\"node-attachments\">");
        foreach (var attachment in attachments)
        {
            AppendAttachment(AttachmentExportHelper.Build(node, attachment, options), sb, indentationLevel + 1);
        }

        AppendLine(sb, indentationLevel, "</div>");
    }

    private static void AppendBlockBody(Node node, StringBuilder sb, int indentationLevel)
    {
        if (node.NodeType != NodeType.TextBlockItem)
            return;

        AppendLine(
            sb,
            indentationLevel,
            $"<blockquote class=\"node-block-quote\">{WebUtility.HtmlEncode(node.Name ?? string.Empty)}</blockquote>");
    }

    private static void AppendAttachment(AttachmentExportItem attachment, StringBuilder sb, int indentationLevel)
    {
        switch (attachment.Kind)
        {
            case AttachmentExportKind.Text:
                AppendLine(
                    sb,
                    indentationLevel,
                    $"<blockquote class=\"attachment-quote\">{WebUtility.HtmlEncode(attachment.TextContent ?? string.Empty)}</blockquote>");
                break;
            case AttachmentExportKind.Image:
            {
                var encodedPath = WebUtility.HtmlEncode(attachment.RelativePath);
                var encodedAlt = WebUtility.HtmlEncode(attachment.DisplayName);
                AppendLine(sb, indentationLevel, "<div class=\"attachment-image-box\">");
                AppendLine(
                    sb,
                    indentationLevel + 1,
                    $"<a class=\"attachment-image-link\" href=\"{encodedPath}\"><img class=\"attachment-image\" src=\"{encodedPath}\" alt=\"{encodedAlt}\" /></a>");
                AppendLine(sb, indentationLevel, "</div>");
                break;
            }
            default:
            {
                var encodedPath = WebUtility.HtmlEncode(attachment.RelativePath);
                var encodedLabel = WebUtility.HtmlEncode(attachment.DisplayName);
                AppendLine(
                    sb,
                    indentationLevel,
                    $"<div class=\"attachment-link\"><a href=\"{encodedPath}\">{encodedLabel}</a></div>");
                break;
            }
        }
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
        AppendLine(sb, 3, ".node-content { max-width: 100%; }");
        AppendLine(sb, 3, ".node-text { min-width: 0; }");
        AppendLine(
            sb,
            3,
            useBlackBackground
                ? ".node-block-quote { margin: 0.55rem 0 0; padding: 0.7rem 0.95rem; border-left: 3px solid #475569; background: rgba(148, 163, 184, 0.12); border-radius: 0.6rem; white-space: pre-wrap; }"
                : ".node-block-quote { margin: 0.55rem 0 0; padding: 0.7rem 0.95rem; border-left: 3px solid #cbd5e1; background: #f8fafc; border-radius: 0.6rem; white-space: pre-wrap; }");
        AppendLine(sb, 3, ".node-attachments { margin-top: 0.55rem; }");
        AppendLine(
            sb,
            3,
            useBlackBackground
                ? ".attachment-quote { margin: 0.5rem 0 0; padding: 0.6rem 0.9rem; border-left: 3px solid #334155; background: rgba(148, 163, 184, 0.12); border-radius: 0.6rem; white-space: pre-wrap; }"
                : ".attachment-quote { margin: 0.5rem 0 0; padding: 0.6rem 0.9rem; border-left: 3px solid #cbd5e1; background: #f8fafc; border-radius: 0.6rem; white-space: pre-wrap; }");
        AppendLine(sb, 3, ".attachment-image-box { margin-top: 0.65rem; max-width: min(100%, 42rem); }");
        AppendLine(sb, 3, ".attachment-image-link { display: block; }");
        AppendLine(sb, 3, ".attachment-image { display: block; width: 100%; height: auto; border-radius: 0.9rem; }");
        AppendLine(sb, 3, ".attachment-link { margin-top: 0.55rem; }");
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
