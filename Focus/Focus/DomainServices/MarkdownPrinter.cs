using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices
{
    internal static class MarkdownPrinter
    {
        private const string Indentation = "    ";
        private const string UntitledNodeName = "Untitled";

        public static void Print(Node node, StringBuilder sb)
        {
            sb.Append("# ");
            sb.AppendLine(NormalizeNodeName(node.Name));

            var visibleChildren = GetVisibleChildren(node).ToArray();
            if (!visibleChildren.Any())
                return;

            sb.AppendLine();
            PrintChildren(visibleChildren, level: 1, sb);
        }

        private static void PrintChildren(IReadOnlyList<Node> children, int level, StringBuilder sb)
        {
            for (var index = 0; index < children.Count; index++)
            {
                PrintNode(children[index], level, index + 1, sb);
            }
        }

        private static void PrintNode(Node node, int level, int visibleIndex, StringBuilder sb)
        {
            var indent = new string(' ', (level - 1) * Indentation.Length);
            var prefix = level == 1
                ? $"{visibleIndex}. "
                : "- ";

            sb.Append(indent);
            sb.Append(prefix);
            sb.AppendLine(NormalizeNodeName(node.Name));

            var visibleChildren = GetVisibleChildren(node).ToArray();
            if (!visibleChildren.Any())
                return;

            PrintChildren(visibleChildren, level + 1, sb);
        }

        private static IEnumerable<Node> GetVisibleChildren(Node node) =>
            node.Children.Where(child => child.NodeType != NodeType.IdeaBagItem);

        private static string NormalizeNodeName(string nodeName)
        {
            if (string.IsNullOrWhiteSpace(nodeName))
                return UntitledNodeName;

            return nodeName.ReplaceLineEndings(" ").Trim();
        }
    }
}
