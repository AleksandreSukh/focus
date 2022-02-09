using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus
{
    public class Node
    {
        private Node _parentNode;
        public Node()
        {
            Name = "";
            Children = new();
        }

        public Node(string name, int number) : this()
        {
            Name = name;
            Number = number;
        }

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

        public void Add(string input)
        {
            var number = Children.Count + 1;
            AddNode(new Node(input, number));
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
            var numberString = level == 1
                ? $"-> {AccessibleKeyNumbering.GetStringFor(Number)}/{Number}. "
                : $"{Number}. ";

            var content = Collapsed
                ? $"{Name} (+)"
                : Children.Any()
                ? $"{Name} (-)"
                    : Name;

            var contentLine = $"{numberString}{content}";

            if (indent.Length + contentLine.Length <= maxWidth)
            {
                sb.Append(indent);
                sb.AppendLine(contentLine);
            }
            else WordWrap(sb, indent, maxWidth, contentLine);

            indent += "    ";

            if (Collapsed && level > 0) return;
            for (int i = 0; i < Children.Count; i++)
                Children[i].Print(indent, i == Children.Count - 1, level + 1, sb, maxWidth);
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