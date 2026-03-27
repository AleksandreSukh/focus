#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Systems.Sanity.Focus.Domain;

public class Node
{
    private Node? _parentNode;

    public Node()
    {
        UniqueIdentifier = Guid.NewGuid();
        Name = string.Empty;
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

    public List<Node> Children { get; }

    public Dictionary<Guid, Link> Links { get; }

    public int Number { get; set; }

    public bool Collapsed { get; set; }

    public TaskState TaskState { get; set; }

    public bool IsCollapsed() => Collapsed && _parentNode != null;

    public Node? GetParent() => _parentNode;

    public void SetParent(Node parent)
    {
        _parentNode = parent;
    }

    public void Add(string input, NodeType nodeType = NodeType.TextItem)
    {
        var number = Children.Count(node => node.NodeType == nodeType) + 1;
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
        string? metadata = null)
    {
        linkedNode.UniqueIdentifier ??= Guid.NewGuid();
        Links[linkedNode.UniqueIdentifier.Value] =
            new Link(linkedNode.UniqueIdentifier.Value, relationType, metadata);
    }

    public void EditNode(string newString)
    {
        Name = SanitizeText(newString);
    }

    public int GetTotalSize()
    {
        return !Children.Any() ? 1 : Children.Sum(child => child.GetTotalSize());
    }

    public void RenumberChildNodes()
    {
        var childNodes = Children.OrderBy(childNode => childNode.Number).ToArray();
        for (var index = 0; index < childNodes.Length; index++)
        {
            childNodes[index].Number = index + 1;
        }
    }

    public bool SanitizeName()
    {
        var sanitizedName = SanitizeText(Name);
        if (Name == sanitizedName)
            return false;

        Name = sanitizedName;
        return true;
    }

    internal void Collapse()
    {
        Collapsed = true;
    }

    internal void Expand()
    {
        Collapsed = false;
    }

    private void AddNode(Node childNode)
    {
        childNode.UniqueIdentifier ??= Guid.NewGuid();
        childNode.Name = SanitizeText(childNode.Name);
        childNode.SetParent(this);
        Children.Add(childNode);
    }

    private static string SanitizeText(string? input)
    {
        if (input == null)
            return string.Empty;

        return new string(input
            .Where(character => !char.IsControl(character) || character == '\r' || character == '\n' || character == '\t')
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
