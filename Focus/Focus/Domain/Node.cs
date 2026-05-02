#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

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
        Metadata = NodeMetadata.Create(NodeMetadataSources.Manual, Environment.MachineName);
    }

    public NodeType NodeType { get; set; }

    public Guid? UniqueIdentifier { get; set; }

    public string Name { get; set; }

    public List<Node> Children { get; }

    public Dictionary<Guid, Link> Links { get; }

    public int Number { get; set; }

    public bool Collapsed { get; set; }

    public bool HideDoneTasks { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? HideDoneTasksExplicit { get; set; }

    public TaskState TaskState { get; set; }

    public NodeMetadata? Metadata { get; set; }

    public bool IsCollapsed() => Collapsed && _parentNode != null;

    public Node? GetParent() => _parentNode;

    public void SetParent(Node? parent)
    {
        _parentNode = parent;
    }

    public Node Add(
        string input,
        NodeType nodeType = NodeType.TextItem,
        string source = NodeMetadataSources.Manual,
        string? device = null)
    {
        var numberingGroup = GetNumberingGroup(nodeType);
        var number = Children.Count(node => GetNumberingGroup(node.NodeType) == numberingGroup) + 1;
        var childNode = new Node(input, nodeType, number);
        childNode.Metadata = NodeMetadata.Create(source, device ?? Environment.MachineName);
        AddNode(childNode);
        return childNode;
    }

    public Node Add(Node childNode)
    {
        var number = Children.Count + 1;
        childNode.Number = number;
        AddNode(childNode);
        return childNode;
    }

    public void AddLink(
        Node linkedNode,
        LinkRelationType relationType = LinkRelationType.Relates,
        string? metadata = null)
    {
        linkedNode.UniqueIdentifier ??= Guid.NewGuid();
        Links[linkedNode.UniqueIdentifier.Value] =
            new Link(linkedNode.UniqueIdentifier.Value, relationType, metadata);
        TouchMetadata();
    }

    public void EditNode(string newString)
    {
        Name = SanitizeText(newString);
        TouchMetadata();
    }

    public int GetTotalSize()
    {
        return !Children.Any() ? 1 : Children.Sum(child => child.GetTotalSize());
    }

    public void RenumberChildNodes()
    {
        RenumberChildNodes(GetNumberingGroup(NodeType.TextItem));
        RenumberChildNodes(GetNumberingGroup(NodeType.IdeaBagItem));
    }

    public bool RemoveDeadLinks(ISet<Guid> liveNodeIds)
    {
        var deadKeys = Links.Keys.Where(k => !liveNodeIds.Contains(k)).ToList();
        if (deadKeys.Count == 0)
            return false;

        foreach (var key in deadKeys)
            Links.Remove(key);

        return true;
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
        TouchMetadata();
    }

    internal void Expand()
    {
        Collapsed = false;
        TouchMetadata();
    }

    internal void SetHideDoneTasks(bool hideDoneTasks)
    {
        HideDoneTasks = hideDoneTasks;
        HideDoneTasksExplicit = true;
        TouchMetadata();
    }

    internal bool? GetHideDoneTasksOverride()
    {
        if (HideDoneTasksExplicit == true)
            return HideDoneTasks;

        return HideDoneTasks
            ? true
            : null;
    }

    internal bool ClearHideDoneTasksOverride()
    {
        var changed = HideDoneTasks || HideDoneTasksExplicit.HasValue;
        if (!changed)
            return false;

        HideDoneTasks = false;
        HideDoneTasksExplicit = null;
        TouchMetadata();
        return true;
    }

    internal void EnsureMetadata(
        string source = NodeMetadataSources.Manual,
        string? device = null,
        DateTimeOffset? timestampUtc = null)
    {
        Metadata ??= NodeMetadata.Create(source, device ?? Environment.MachineName, timestampUtc);
    }

    internal void BackfillMetadata(DateTimeOffset timestampUtc, string source)
    {
        Metadata ??= NodeMetadata.Create(source, device: null, timestampUtc);
    }

    internal void TouchMetadata(DateTimeOffset? timestampUtc = null)
    {
        EnsureMetadata(timestampUtc: timestampUtc);
        Metadata!.Touch(timestampUtc);
    }

    internal void AddAttachment(NodeAttachment attachment, DateTimeOffset? timestampUtc = null)
    {
        EnsureMetadata(timestampUtc: timestampUtc);
        Metadata!.Attachments.Add(attachment);
        Metadata.Touch(timestampUtc);
    }

    private void AddNode(Node childNode)
    {
        childNode.UniqueIdentifier ??= Guid.NewGuid();
        childNode.Name = SanitizeText(childNode.Name);
        childNode.EnsureMetadata();
        childNode.SetParent(this);
        Children.Add(childNode);
        TouchMetadata();
    }

    private static string SanitizeText(string? input)
    {
        if (input == null)
            return string.Empty;

        return new string(input
            .Where(character => !char.IsControl(character) || character == '\r' || character == '\n' || character == '\t')
            .ToArray());
    }

    private void RenumberChildNodes(NodeType numberingGroup)
    {
        var childNodes = Children
            .Where(childNode => GetNumberingGroup(childNode.NodeType) == numberingGroup)
            .OrderBy(childNode => childNode.Number)
            .ToArray();

        for (var index = 0; index < childNodes.Length; index++)
        {
            childNodes[index].Number = index + 1;
        }
    }

    private static NodeType GetNumberingGroup(NodeType nodeType) =>
        nodeType == NodeType.IdeaBagItem
            ? NodeType.IdeaBagItem
            : NodeType.TextItem;
}

public static class NodeMetadataSources
{
    public const string Manual = "manual";
    public const string ClipboardText = "clipboard-text";
    public const string ClipboardImage = "clipboard-image";
    public const string LegacyImport = "legacy-import";
}

public enum NodeType
{
    TextItem,
    IdeaBagItem,
    TextBlockItem
}

public record Link(
    Guid id,
    LinkRelationType relationType = LinkRelationType.Relates,
    string? metadata = null);
