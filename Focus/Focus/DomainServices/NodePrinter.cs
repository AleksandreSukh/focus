using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.DomainServices
{
    internal class NodePrinter
    {
        public static void Print(Node node, string indent, bool last, int level, StringBuilder sb, int maxWidth)
        {
            bool hasChildren = node.Children.Count > 0;
            bool isTopLevel = level == 1;
            bool isEvenLevel = level % 2 == 0;

            var isEndOfBranch = !hasChildren && (last || isTopLevel);
            //TODO; we shouldn't have sub levels of idea tags for now
            if (node.NodeType == NodeType.IdeaBagItem)
                return;

            var numberString = isTopLevel
                //? $"-> {AccessibleKeyNumbering.GetStringFor(Number)}/{Number}. " //TODO: temporary feature - to be refactored
                ? $"-> [{ConfigurationConstants.CommandColor}]{AccessibleKeyNumbering.GetStringFor(node.Number)}[!]/[{ConfigurationConstants.CommandColor}]{node.Number}[!]. "
                : isEvenLevel ? "* " : "• "; //TODO:Extract const chars to separate class

            var content = new StringBuilder(numberString);
            content.Append(node.Name);

            if (node.Collapsed && level > 0)
            {
                content.Append($" {new string('/', node.GetTotalSize())}");
            }
            // else if (Children.Any())
            // {
            //     content.Append(" (-)");
            // }

            PrintLinks(node, indent + new string(' ', numberString.Length), sb, maxWidth);
            PrintIdeaTags(node, indent + new string(' ', numberString.Length), sb, maxWidth);

            var nodeToPrint = content.ToString();
            PrintWithIndentation(nodeToPrint, indent, sb, maxWidth);

            if (isEndOfBranch)
                sb.AppendLine(":"); //TODO:

            indent += "    ";

            if (node.Collapsed && level > 0) return;
            for (int i = 0; i < node.Children.Count; i++)
                Print(node.Children[i], indent, i == node.Children.Count - 1, level + 1, sb, maxWidth);
        }

        private static void PrintLinks(Node node, string indent, StringBuilder sb, int maxWidth)
        {
            string GetShortPeek(string nodeName, int peekContentLength) =>
                nodeName.Length <= peekContentLength
                    ? nodeName
                    : nodeName.Substring(0, peekContentLength) + "...";

            if (node.Links.Any())
            {
                var linksStringBuilder = new StringBuilder();
                linksStringBuilder.Append("[green]");
                linksStringBuilder.Append("{");
                foreach (var link in node.Links)
                {
                    var linkedItemName = GlobalLinkDitionary.Nodes[link.Key];
                    linksStringBuilder.Append($" *({GetShortPeek(linkedItemName.Name, 5)})* ");
                }

                linksStringBuilder.Append("}");
                linksStringBuilder.Append("[!]");

                PrintWithIndentation(linksStringBuilder.ToString(), indent, sb, maxWidth);
            }
        }

        private static void PrintIdeaTags(Node node, string indent, StringBuilder sb, int maxWidth)
        {
            var ideaTags = node.Children.Where(c => c.NodeType == NodeType.IdeaBagItem).ToArray();
            if (ideaTags.Any())
            {
                var ideaTagsStringBuilder = new StringBuilder();
                ideaTagsStringBuilder.Append("[yellow]");
                ideaTagsStringBuilder.Append("{");
                foreach (var ideatag in ideaTags)
                {
                    ideaTagsStringBuilder.Append($" *({ideatag.Name})* ");
                }

                ideaTagsStringBuilder.Append("}");
                ideaTagsStringBuilder.Append("[!]");

                PrintWithIndentation(ideaTagsStringBuilder.ToString(), indent, sb, maxWidth);
            }
        }
        private static void PrintWithIndentation(string text, string indent, StringBuilder sb, int maxWidth)
        {
            if (indent.Length + text.Length <= maxWidth)
            {
                sb.Append(indent);
                sb.AppendLine(text);
            }
            else WordWrap(sb, indent, maxWidth, text);
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
                if (word.Length + 1 < spaceHasLeft) // 1 char will be used for space
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
}
