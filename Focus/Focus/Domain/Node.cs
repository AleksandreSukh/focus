using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Domain
{
    public class Node
    {
        private Node _parentNode;
        public Node()
        {
            Name = "";
            Children = new();
        }

        public Node(string name, NodeType nodeType, int number) : this()
        {
            Name = name;
            NodeType = nodeType;
            Number = number;
        }

        public NodeType NodeType { get; set; }

        public string Name { get; set; }
        //TODO we could optimize performance by making this a dictionary (see usages)
        public List<Node> Children { get; set; }
        public int Number { get; set; }
        public bool Collapsed { get; set; }

        public void SetParent(Node parent)
        {
            _parentNode = parent;
        }

        public Node GetParent() => _parentNode;

        public void Add(string input, NodeType nodeType = NodeType.TextItem)
        {
            var number = Children.Count(i => i.NodeType == nodeType) + 1;
            AddNode(new Node(input, nodeType, number));
        }

        public void Add(Node childNode)
        {
            var number = Children.Count + 1;
            childNode.Number = number;
            AddNode(childNode);
        }

        private void AddNode(Node childNode)
        {
            Children.Add(childNode);
        }

        //public override string ToString()
        //{
        //    return JsonConvert.SerializeObject(this);
        //}
        //TODO: Refactor - take to infrastructure
        public void Print(string indent, bool last, int level, StringBuilder sb, int maxWidth)
        {
            //TODO; we shouldn't have sub levels of idea tags for now
            if (NodeType == NodeType.IdeaBagItem)
                return;



            var numberString = level == 1
                ? $"-> {AccessibleKeyNumbering.GetStringFor(Number)}/{Number}. "
                : $"{Number}. ";

            var content = Collapsed
                ? $"{Name} (+)"
                : Children.Any()
                ? $"{Name} (-)"
                    : Name;

            var contentLine = $"{numberString}{content}";

            PrintIdeaTags(indent + new string(' ', numberString.Length), sb, maxWidth);

            PrintWithIndentation(contentLine, indent, sb, maxWidth);

            indent += "    ";

            if (Collapsed && level > 0) return;
            for (int i = 0; i < Children.Count; i++)
                Children[i].Print(indent, i == Children.Count - 1, level + 1, sb, maxWidth);
        }

        private void PrintIdeaTags(string indent, StringBuilder sb, int maxWidth)
        {
            var ideaTags = Children.Where(c => c.NodeType == NodeType.IdeaBagItem);
            if (ideaTags.Any())
            {
                var ideaTagsString = ideaTags.Select(i => $"[darkyellow]*({i.Name})*[!]")
                    .JoinString();
                PrintWithIndentation(ideaTagsString, indent, sb, maxWidth);
            }
        }

        private void PrintWithIndentation(string text, string indent, StringBuilder sb, int maxWidth)
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

    public enum NodeType
    {
        TextItem,
        IdeaBagItem
    }
}