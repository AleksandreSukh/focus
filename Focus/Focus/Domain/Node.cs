using System;
using System.Collections.Generic;
using System.Linq;

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

    public int GetTotalSize()
    {
        return !Children.Any() ? 1 : Children.Sum(c => c.GetTotalSize());
    }

    public NodeType NodeType { get; }
    public Guid? UniqueIdentifier { get; set; }
    public string Name { get; set; }
    //TODO we could optimize performance by making this a dictionary (see usages)
    public List<Node> Children { get; }
    public Dictionary<Guid, Link> Links { get; }
    public int Number { get; set; }
    public bool Collapsed { get; set; }

    public void RenumberChildNodes()
    {
        var childNodes = Children.OrderBy(cn => cn.Number).ToArray();
        for (int i = 0; i < childNodes.Count(); i++)
        {
            childNodes[i].Number = i + 1;
        }
    }
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