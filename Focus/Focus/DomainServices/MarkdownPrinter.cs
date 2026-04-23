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
            var ancestorHidesDone = NodeBranchVisibility.HideDoneStateForNode(node);
            var hasRootBlockBody = node.NodeType == NodeType.TextBlockItem;

            sb.Append("# ");
            sb.AppendLine(FormatNodeName(node));

            var rootAttachments = NodeExportHelpers.GetAttachments(node, options);
            var visibleChildren = NodeExportHelpers.GetVisibleChildren(node, ancestorHidesDone).ToArray();
            if (!hasRootBlockBody && !rootAttachments.Any() && !visibleChildren.Any())
                return;

            sb.AppendLine();
            AppendBlockBody(node, level: 0, sb, isRoot: true);
            if (hasRootBlockBody && (rootAttachments.Any() || visibleChildren.Any()))
                sb.AppendLine();

            AppendAttachments(node, rootAttachments, level: 0, sb, options, isRoot: true);
            if ((hasRootBlockBody || rootAttachments.Any()) && visibleChildren.Any())
                sb.AppendLine();

            if (!visibleChildren.Any())
                return;

            PrintChildren(node, visibleChildren, level: 1, sb, options, ancestorHidesDone);
        }

        private static void PrintChildren(
            Node parent,
            IReadOnlyList<Node> children,
            int level,
            StringBuilder sb,
            NodeExportOptions options,
            bool ancestorHidesDone)
        {
            var hideDoneStateForChildren = NodeBranchVisibility.HideDoneStateForChildren(parent, ancestorHidesDone);
            for (var index = 0; index < children.Count; index++)
            {
                PrintNode(children[index], level, index + 1, sb, options, hideDoneStateForChildren);
            }
        }

        private static void PrintNode(
            Node node,
            int level,
            int visibleIndex,
            StringBuilder sb,
            NodeExportOptions options,
            bool ancestorHidesDone)
        {
            var indent = new string(' ', (level - 1) * Indentation.Length);
            var prefix = level == 1
                ? $"{visibleIndex}. "
                : "- ";

            sb.Append(indent);
            sb.Append(prefix);
            sb.AppendLine(FormatNodeName(node));

            AppendBlockBody(node, level, sb, isRoot: false);
            AppendAttachments(node, NodeExportHelpers.GetAttachments(node, options), level, sb, options, isRoot: false);

            var visibleChildren = NodeExportHelpers.GetVisibleChildren(node, ancestorHidesDone).ToArray();
            if (!visibleChildren.Any())
                return;

            if (options.SkipCollapsedDescendants && node.IsCollapsed())
                return;

            PrintChildren(node, visibleChildren, level + 1, sb, options, ancestorHidesDone);
        }

        private static void AppendAttachments(
            Node node,
            IReadOnlyList<NodeAttachment> attachments,
            int level,
            StringBuilder sb,
            NodeExportOptions options,
            bool isRoot)
        {
            if (attachments.Count == 0)
                return;

            foreach (var attachment in attachments)
            {
                AppendAttachment(AttachmentExportHelper.Build(node, attachment, options), level, sb, isRoot);
            }
        }

        private static void AppendAttachment(
            AttachmentExportItem attachment,
            int level,
            StringBuilder sb,
            bool isRoot)
        {
            switch (attachment.Kind)
            {
                case AttachmentExportKind.Text:
                    AppendQuotedText(attachment.TextContent ?? string.Empty, level, sb, isRoot);
                    break;
                case AttachmentExportKind.Image:
                    AppendIndentedLine(BuildMarkdownImage(attachment), level, sb, isRoot);
                    break;
                default:
                    AppendIndentedLine(BuildMarkdownLink(attachment), level, sb, isRoot);
                    break;
            }
        }

        private static void AppendBlockBody(Node node, int level, StringBuilder sb, bool isRoot)
        {
            if (node.NodeType != NodeType.TextBlockItem)
                return;

            AppendQuotedText(node.Name, level, sb, isRoot);
        }

        private static void AppendQuotedText(string text, int level, StringBuilder sb, bool isRoot)
        {
            var quoteIndent = BuildAttachmentIndent(level, isRoot);
            var lines = (text ?? string.Empty).ReplaceLineEndings("\n").Split('\n');
            foreach (var line in lines)
            {
                sb.Append(quoteIndent);
                sb.Append("> ");
                sb.AppendLine(EscapeMarkdownLiteral(line));
            }
        }

        private static void AppendIndentedLine(string line, int level, StringBuilder sb, bool isRoot)
        {
            sb.Append(BuildAttachmentIndent(level, isRoot));
            sb.AppendLine(line);
        }

        private static string BuildAttachmentIndent(int level, bool isRoot) =>
            isRoot
                ? string.Empty
                : new string(' ', level * Indentation.Length);

        private static string BuildMarkdownImage(AttachmentExportItem attachment)
        {
            var label = EscapeMarkdownLabel(attachment.DisplayName);
            var destination = FormatMarkdownDestination(attachment.RelativePath);
            return $"[![{label}]({destination})]({destination})";
        }

        private static string BuildMarkdownLink(AttachmentExportItem attachment) =>
            $"[{EscapeMarkdownLabel(attachment.DisplayName)}]({FormatMarkdownDestination(attachment.RelativePath)})";

        private static string FormatMarkdownDestination(string relativePath) =>
            $"<{relativePath.Replace(">", "\\>", System.StringComparison.Ordinal)}>";

        private static string EscapeMarkdownLabel(string value) =>
            EscapeMarkdownLiteral(value);

        private static string EscapeMarkdownLiteral(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var escaped = new StringBuilder(value.Length * 2);
            foreach (var character in value)
            {
                if (character is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '(' or ')' or '#' or '+' or '-' or '!' or '|' or '>')
                    escaped.Append('\\');

                escaped.Append(character);
            }

            return EscapeLeadingOrderedList(escaped.ToString());
        }

        private static string EscapeLeadingOrderedList(string value)
        {
            if (string.IsNullOrEmpty(value) || !char.IsDigit(value[0]))
                return value;

            var index = 0;
            while (index < value.Length && char.IsDigit(value[index]))
            {
                index++;
            }

            if (index > 0 &&
                index + 1 < value.Length &&
                value[index] == '.' &&
                value[index + 1] == ' ')
            {
                return $"{value[..index]}\\.{value[(index + 1)..]}";
            }

            return value;
        }

        private static string FormatNodeName(Node node) =>
            PlainTextInlineFormatter.ToPlainText(NodeExportHelpers.FormatNodeName(node));
    }
}
