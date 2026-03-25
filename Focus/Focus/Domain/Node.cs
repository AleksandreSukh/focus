using System;
using System.Collections.Generic;
using System.Linq;

namespace Systems.Sanity.Focus.Domain;

public class Node
{
    private Node _parentNode;

    public Node()
    {
        UniqueIdentifier = Guid.NewGuid();
        Name = "";
        Children = new();
        Links = new();
    }

    public Node(string name, NodeType nodeType, int number) : this()
    {
        Name = SanitizeText(name);
        NodeType = nodeType;
        Number = number;
    }

    public NodeType NodeType { get; }
    public Guid? UniqueIdentifier { get; set; }
    public string Name { get; set; }
    //TODO we could optimize performance by making this a dictionary (see usages)
    public List<Node> Children { get; }
    public Dictionary<Guid, Link> Links { get; }
    public int Number { get; set; }
    public bool Collapsed { get; set; }

    public bool IsCollapsed() => Collapsed && _parentNode != null;

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

    public void AddLink(
        Node linkedNode,
        LinkRelationType relationType = LinkRelationType.Relates,
        string metadata = null)
    {
        linkedNode.UniqueIdentifier ??= Guid.NewGuid();
        Links[linkedNode.UniqueIdentifier.Value] =
            new Link(linkedNode.UniqueIdentifier.Value, relationType, metadata);
    }

    private void AddNode(Node childNode)
    {
        childNode.UniqueIdentifier ??= Guid.NewGuid();
        childNode.Name = SanitizeText(childNode.Name);
        childNode.SetParent(this);
        Children.Add(childNode);
    }

    public void EditNode(string newString)
    {
        Name = SanitizeText(newString);
    }

    public bool SanitizeName()
    {
        var sanitizedName = SanitizeText(Name);
        if (Name == sanitizedName)
            return false;

        Name = sanitizedName;
        return true;
    }

    public int GetTotalSize()
    {
        return !Children.Any() ? 1 : Children.Sum(c => c.GetTotalSize());
    }

    public void RenumberChildNodes()
    {
        var childNodes = Children.OrderBy(cn => cn.Number).ToArray();
        for (int i = 0; i < childNodes.Count(); i++)
        {
            childNodes[i].Number = i + 1;
        }
    }

    internal void Collapse()
    {
        Collapsed = true;
    }

    internal void Expand()
    {
        Collapsed = false;
    }

    private static string SanitizeText(string input)
    {
        if (input == null)
            return string.Empty;

        return new string(input
            .Where(c => !char.IsControl(c) || c == '\r' || c == '\n' || c == '\t')
            .ToArray());
    }
}

public enum NodeType
{
    TextItem,
    IdeaBagItem
}
public record Link(
    Guid id,
    LinkRelationType relationType = LinkRelationType.Relates,
    string? metadata = null);

public static class GlobalLinkDitionary
{
    public static readonly Dictionary<Guid, Node> Nodes = new Dictionary<Guid, Node>();
    public static readonly Dictionary<Guid, string> NodeFiles = new Dictionary<Guid, string>();
    public static readonly Dictionary<Guid, HashSet<Guid>> Backlinks = new Dictionary<Guid, HashSet<Guid>>();
    public static readonly Stack<Node> NodesToBeLinked = new Stack<Node>();
}
