using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices
{
    internal static class MarkdownPrinter
    {
        private const string Indentation = "    ";

        public static void Print(Node node, StringBuilder sb, NodeExportOptions options = null)
        {
            options ??= new NodeExportOptions();

            sb.Append("# ");
            sb.AppendLine(FormatNodeName(node));

            var visibleChildren = NodeExportHelpers.GetVisibleChildren(node).ToArray();
            if (!visibleChildren.Any())
                return;

            sb.AppendLine();
            PrintChildren(visibleChildren, level: 1, sb, options);
        }

        private static void PrintChildren(
            IReadOnlyList<Node> children,
            int level,
            StringBuilder sb,
            NodeExportOptions options)
        {
            for (var index = 0; index < children.Count; index++)
            {
                PrintNode(children[index], level, index + 1, sb, options);
            }
        }

        private static void PrintNode(
            Node node,
            int level,
            int visibleIndex,
            StringBuilder sb,
            NodeExportOptions options)
        {
            var indent = new string(' ', (level - 1) * Indentation.Length);
            var prefix = level == 1
                ? $"{visibleIndex}. "
                : "- ";

            sb.Append(indent);
            sb.Append(prefix);
            sb.AppendLine(FormatNodeName(node));

            var visibleChildren = NodeExportHelpers.GetVisibleChildren(node).ToArray();
            if (!visibleChildren.Any())
                return;

            if (options.SkipCollapsedDescendants && node.IsCollapsed())
                return;

            PrintChildren(visibleChildren, level + 1, sb, options);
        }

        private static string FormatNodeName(Node node) =>
            PlainTextInlineFormatter.ToPlainText(NodeExportHelpers.FormatNodeName(node));
    }
}
