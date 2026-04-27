#nullable enable

using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class PlainTextPrinter
{
    private const int IndentationWidth = 2;

    public static void Print(Node node, StringBuilder sb, NodeExportOptions? options = null)
    {
        options ??= new NodeExportOptions();
        var ancestorHidesDone = NodeBranchVisibility.HideDoneStateForNode(node);

        sb.AppendLine(FormatNodeName(node));
        AppendBlockBody(node, indentationLevel: 0, sb);

        var visibleChildren = NodeExportHelpers.GetVisibleChildren(node, ancestorHidesDone).ToArray();
        if (!visibleChildren.Any())
            return;

        PrintChildren(node, visibleChildren, level: 1, sb, options, ancestorHidesDone);
    }

    private static void PrintChildren(
        Node parent,
        Node[] children,
        int level,
        StringBuilder sb,
        NodeExportOptions options,
        bool ancestorHidesDone)
    {
        var hideDoneStateForChildren = NodeBranchVisibility.HideDoneStateForChildren(parent, ancestorHidesDone);
        foreach (var child in children)
        {
            PrintNode(child, level, sb, options, hideDoneStateForChildren);
        }
    }

    private static void PrintNode(
        Node node,
        int level,
        StringBuilder sb,
        NodeExportOptions options,
        bool ancestorHidesDone)
    {
        AppendIndent(sb, level - 1);
        sb.Append("- ");
        sb.AppendLine(FormatNodeName(node));
        AppendBlockBody(node, level, sb);

        var visibleChildren = NodeExportHelpers.GetVisibleChildren(node, ancestorHidesDone).ToArray();
        if (!visibleChildren.Any())
            return;

        if (options.SkipCollapsedDescendants && node.IsCollapsed())
            return;

        PrintChildren(node, visibleChildren, level + 1, sb, options, ancestorHidesDone);
    }

    private static void AppendBlockBody(Node node, int indentationLevel, StringBuilder sb)
    {
        if (node.NodeType != NodeType.TextBlockItem)
            return;

        foreach (var line in NodeDisplayHelper.GetMultilineLines(node.Name))
        {
            AppendIndent(sb, indentationLevel);
            sb.Append("> ");
            sb.AppendLine(PlainTextInlineFormatter.ToPlainText(line));
        }
    }

    private static string FormatNodeName(Node node) =>
        PlainTextInlineFormatter.ToPlainText(NodeExportHelpers.FormatNodeName(node));

    private static void AppendIndent(StringBuilder sb, int indentationLevel)
    {
        if (indentationLevel <= 0)
            return;

        sb.Append(' ', indentationLevel * IndentationWidth);
    }
}
