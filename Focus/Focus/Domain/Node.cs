using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Domain;

public class Node
{
    private Node _parentNode;
    public Node()
    {
        Name = "";
        Children = new();
        Links = new();
    }

    public Node(string name, NodeType nodeType, int number) : this()
    {
        Name = name;
        NodeType = nodeType;
        Number = number;
    }

    public Node GetParent() => _parentNode;
    public void SetParent(Node parent)
    {
        _parentNode = parent;
    }

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

    public void AddLink(Node linkedNode, string metadata = null)
    {
        Links.TryAdd(linkedNode.UniqueIdentifier!.Value, new Link(linkedNode.UniqueIdentifier.Value, metadata));
    }

    private void AddNode(Node childNode)
    {
        Children.Add(childNode);
    }

    public void EditNode(string newString)
    {
        Name = newString;
    }

    //TODO: Refactor - take to infrastructure
    public void Print(string indent, bool last, int level, StringBuilder sb, int maxWidth)
    {
        //TODO; we shouldn't have sub levels of idea tags for now
        if (NodeType == NodeType.IdeaBagItem)
            return;

        var numberString = level == 1
            ? $"-> {AccessibleKeyNumbering.GetStringFor(Number)}/{Number}. "
            : "* ";

        var content = new StringBuilder(numberString);
        content.Append(Name);

        if (Collapsed && level > 0)
        {
            content.Append($" (+) {new string('#', GetTotalSize())}");
        }
        else if (Children.Any())
        {
            content.Append(" (-)");
        }

        PrintLinks(indent + new string(' ', numberString.Length), sb, maxWidth);
        PrintIdeaTags(indent + new string(' ', numberString.Length), sb, maxWidth);

        PrintWithIndentation(content.ToString(), indent, sb, maxWidth);

        indent += "    ";

        if (Collapsed && level > 0) return;
        for (int i = 0; i < Children.Count; i++)
            Children[i].Print(indent, i == Children.Count - 1, level + 1, sb, maxWidth);
    }

    private int GetTotalSize()
    {
        return !Children.Any() ? 1 : Children.Sum(c => c.GetTotalSize());
    }

    private void PrintIdeaTags(string indent, StringBuilder sb, int maxWidth)
    {
        var ideaTags = Children.Where(c => c.NodeType == NodeType.IdeaBagItem).ToArray();
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

    private void PrintLinks(string indent, StringBuilder sb, int maxWidth)
    {
        if (Links.Any())
        {
            var linksStringBuilder = new StringBuilder();
            linksStringBuilder.Append("[green]");
            linksStringBuilder.Append("{");
            foreach (var link in Links)
            {
                var linkedItemName = GlobalLinkDitionary.Nodes[link.Key];
                linksStringBuilder.Append($" *({linkedItemName.GetShortPeek(5)})* ");
            }

            linksStringBuilder.Append("}");
            linksStringBuilder.Append("[!]");

            PrintWithIndentation(linksStringBuilder.ToString(), indent, sb, maxWidth);
        }
    }

    private string GetShortPeek(int peekContentLength)
    {
        var nodeLength = Name.Length;
        var formattedNode = nodeLength <= peekContentLength
            ? Name
            : Name.Substring(0, peekContentLength) + "...";

        return formattedNode;
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

    public NodeType NodeType { get; }
    public Guid? UniqueIdentifier { get; set; }
    public string Name { get; set; }
    //TODO we could optimize performance by making this a dictionary (see usages)
    public List<Node> Children { get; }
    public Dictionary<Guid, Link> Links { get; }
    public int Number { get; set; }
    public bool Collapsed { get; set; }
}

public enum NodeType
{
    TextItem,
    IdeaBagItem
}
public record Link(Guid id, string? metadata = null);

public static class GlobalLinkDitionary
{
    public static readonly Dictionary<Guid, Node> Nodes = new Dictionary<Guid, Node>();
    public static readonly Stack<Node> NodesToBeLinked = new Stack<Node>();
    public static bool LinksLoaded { get; set; }
}