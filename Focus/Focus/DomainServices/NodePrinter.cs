#nullable enable

using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.DomainServices;

internal static class NodePrinter
{
    public static void Print(
        Node node,
        ILinkIndex? linkIndex,
        string indent,
        bool last,
        int level,
        StringBuilder sb,
        int maxWidth,
        bool ancestorHidesDone = false)
    {
        if (level > 0 && NodeBranchVisibility.ShouldHideNode(node, ancestorHidesDone))
            return;

        var visibleChildren = NodeExportHelpers.GetVisibleChildren(node, ancestorHidesDone).ToArray();
        var hasChildren = visibleChildren.Length > 0;
        var isTopLevel = level == 1;
        var isEvenLevel = level % 2 == 0;

        var isEndOfBranch = !hasChildren && (last || isTopLevel);
        if (node.NodeType == NodeType.IdeaBagItem)
            return;

        var numberString = isTopLevel
            ? $"-> [{ConfigurationConstants.CommandColor}]{AccessibleKeyNumbering.GetStringFor(node.Number)}[!]/[{ConfigurationConstants.CommandColor}]{node.Number}[!]. "
            : isEvenLevel ? "* " : "• ";

        var content = new StringBuilder(numberString);
        content.Append(NodeDisplayHelper.BuildDisplayName(node));

        if (node.IsCollapsed() && level > 0)
        {
            content.Append($" {new string('/', node.GetTotalSize())}");
        }

        var linkPeekLength = level == 0 ? 120 : 12;
        PrintLinks(node, linkIndex, indent + new string(' ', numberString.Length), sb, maxWidth, linkPeekLength);
        PrintBacklinks(node, linkIndex, indent + new string(' ', numberString.Length), sb, maxWidth);
        PrintIdeaTags(node, indent + new string(' ', numberString.Length), sb, maxWidth);

        var nodeToPrint = content.ToString();
        PrintWithIndentation(nodeToPrint, indent, sb, maxWidth);

        if (isEndOfBranch)
            sb.AppendLine(ConfigurationConstants.NodePrinting.LeftBorderAtTheEndOfBranch);

        indent += ConfigurationConstants.NodePrinting.TabSpaceForIndentation;

        if (node.IsCollapsed() && level > 0)
            return;

        var hideDoneStateForChildren = NodeBranchVisibility.HideDoneStateForChildren(node, ancestorHidesDone);
        for (var i = 0; i < visibleChildren.Length; i++)
        {
            Print(visibleChildren[i], linkIndex, indent, i == visibleChildren.Length - 1, level + 1, sb, maxWidth, hideDoneStateForChildren);
        }
    }

    private static void PrintLinks(Node node, ILinkIndex? linkIndex, string indent, StringBuilder sb, int maxWidth, int peekLength = 12)
    {
        static string GetShortPeek(string nodeName, int peekContentLength) =>
            nodeName.Length <= peekContentLength
                ? nodeName
                : nodeName.Substring(0, peekContentLength) + "...";

        if (!node.Links.Any())
            return;

        var linksStringBuilder = new StringBuilder();
        linksStringBuilder.Append("[green]");
        linksStringBuilder.Append("{");
        foreach (var link in node.Links.Values)
        {
            if (linkIndex != null && linkIndex.TryGetNode(link.id, out var linkedNode))
            {
                linksStringBuilder.Append(
                    $" *({link.relationType.ToDisplayString()}: {GetShortPeek(linkedNode.Name, peekLength)})* ");
            }
            else
            {
                linksStringBuilder.Append(
                    $" *({link.relationType.ToDisplayString()}: missing:{GetShortPeek(link.id.ToString(), 8)})* ");
            }
        }

        linksStringBuilder.Append("}");
        linksStringBuilder.Append("[!]");
        PrintWithIndentation(linksStringBuilder.ToString(), indent, sb, maxWidth);
    }

    private static void PrintBacklinks(Node node, ILinkIndex? linkIndex, string indent, StringBuilder sb, int maxWidth)
    {
        if (linkIndex == null ||
            !node.UniqueIdentifier.HasValue ||
            !linkIndex.TryGetBacklinkIds(node.UniqueIdentifier.Value, out var backlinks) ||
            backlinks.Count == 0)
        {
            return;
        }

        var backlinksStringBuilder = new StringBuilder();
        backlinksStringBuilder.Append("[cyan]");
        backlinksStringBuilder.Append("{");
        backlinksStringBuilder.Append($" backlinks: {backlinks.Count} ");
        backlinksStringBuilder.Append("}");
        backlinksStringBuilder.Append("[!]");

        PrintWithIndentation(backlinksStringBuilder.ToString(), indent, sb, maxWidth);
    }

    private static void PrintIdeaTags(Node node, string indent, StringBuilder sb, int maxWidth)
    {
        var ideaTags = node.Children.Where(c => c.NodeType == NodeType.IdeaBagItem).ToArray();
        if (!ideaTags.Any())
            return;

        var ideaTagsStringBuilder = new StringBuilder();
        ideaTagsStringBuilder.Append("[yellow]");
        ideaTagsStringBuilder.Append("{");
        foreach (var ideaTag in ideaTags)
        {
            ideaTagsStringBuilder.Append($" *({ideaTag.Name})* ");
        }

        ideaTagsStringBuilder.Append("}");
        ideaTagsStringBuilder.Append("[!]");
        PrintWithIndentation(ideaTagsStringBuilder.ToString(), indent, sb, maxWidth);
    }

    private static void PrintWithIndentation(string text, string indent, StringBuilder sb, int maxWidth)
    {
        if (indent.Length + text.Length <= maxWidth)
        {
            sb.Append(indent);
            sb.AppendLine(text);
            return;
        }

        WordWrap(sb, indent, maxWidth, text);
    }

    private static void WordWrap(StringBuilder sb, string indent, int maxWidth, string contentLine)
    {
        var availableWidthForNodeName = maxWidth - indent.Length;
        var words = contentLine.Split(' ');

        var spaceUsedOnCurrentLine = maxWidth - availableWidthForNodeName;
        sb.Append(indent);
        foreach (var word in words)
        {
            var spaceHasLeft = availableWidthForNodeName - spaceUsedOnCurrentLine;
            if (word.Length + 1 < spaceHasLeft)
            {
                sb.Append(' ');
                sb.Append(word);
                spaceUsedOnCurrentLine += word.Length + 1;
            }
            else
            {
                sb.AppendLine();
                sb.Append(indent);
                sb.Append(word);
                spaceUsedOnCurrentLine = indent.Length + word.Length;
            }
        }

        sb.AppendLine();
    }
}
