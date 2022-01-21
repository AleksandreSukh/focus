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

        public void Print(string indent, bool last, int level, StringBuilder sb)
        {
            sb.Append(indent);
            sb.Append(level == 1
                ? $"-> {AccessibleKeyNumbering.GetStringFor(Number)}/{Number}. "
                : $"{Number}. ");

            if (Collapsed)
                sb.AppendLine($"{Name} (+)");
            else if (Children.Any())
                sb.AppendLine($"{Name} (-)");
            else sb.AppendLine(Name);
            indent += "    ";

            if (Collapsed && level > 0) return;
            for (int i = 0; i < Children.Count; i++)
                Children[i].Print(indent, i == Children.Count - 1, level + 1, sb);
        }
    }
}