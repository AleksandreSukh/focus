#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Application.Llm;

internal sealed class LlmContextBuilder
{
    private static readonly Regex UrlRegex = new(@"\bhttps?://[^\s<>""')\]]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public LlmContextDocument? Build(
        FocusAppContext appContext,
        MindMap selectedMap,
        string selectedMapFilePath,
        Guid nodeId)
    {
        var jobStore = new LlmJobStore(appContext.MapsStorage.UserMindMapsDirectory);
        var selectedFullPath = Path.GetFullPath(selectedMapFilePath);
        var selectedSnapshot = BuildSnapshot(jobStore, selectedFullPath, selectedMap);
        var snapshots = new List<LlmMapSnapshot> { selectedSnapshot };

        foreach (var file in appContext.MapRepository.GetAll().OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = Path.GetFullPath(file.FullName);
            if (string.Equals(fullPath, selectedFullPath, StringComparison.OrdinalIgnoreCase))
                continue;

            snapshots.Add(BuildSnapshot(jobStore, fullPath, appContext.MapRepository.OpenMap(fullPath)));
        }

        return Build(selectedSnapshot, snapshots, nodeId);
    }

    public string ToJson(LlmContextDocument context) =>
        JsonConvert.SerializeObject(context, JsonSerialization.CreateDefaultSettings());

    public string ToMarkdown(LlmContextDocument context)
    {
        var lines = new List<string>
        {
            $"# {context.Prompt.Text}",
            string.Empty,
            $"Map: {context.Map.MapName} ({context.Map.FilePath})",
            $"Node: {context.Prompt.NodeId}",
            $"Path: {context.Prompt.Path}",
            string.Empty,
            "## Tree"
        };

        AppendMarkdownNode(lines, context.Subtree, depth: 0);

        if (context.Links.Outgoing.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Outgoing Links");
            foreach (var link in context.Links.Outgoing)
            {
                lines.Add($"- {link.RelationLabel}: {link.MapName} > {link.NodePath} ({link.NodeId})");
            }
        }

        if (context.Links.Backlinks.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Backlinks");
            foreach (var link in context.Links.Backlinks)
            {
                lines.Add($"- {link.RelationLabel}: {link.MapName} > {link.NodePath} ({link.NodeId})");
            }
        }

        if (context.Urls.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Links Found In Text");
            foreach (var url in context.Urls)
            {
                lines.Add($"- {url.Url}");
            }
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static LlmContextDocument? Build(
        LlmMapSnapshot selectedSnapshot,
        IReadOnlyList<LlmMapSnapshot> snapshots,
        Guid nodeId)
    {
        var selectedPath = FindNodePath(selectedSnapshot.Map.RootNode, nodeId);
        if (selectedPath.Count == 0)
            return null;

        var nodeIndex = BuildNodeIndex(snapshots);
        var promptNode = selectedPath[^1].Node;
        var promptText = LlmPromptService.GetPromptText(promptNode);

        var outgoing = BuildOutgoingLinks(promptNode, nodeIndex);
        var backlinks = BuildBacklinks(promptNode, nodeIndex);

        return new LlmContextDocument
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Map = new LlmContextMap
            {
                FilePath = selectedSnapshot.FilePath,
                FileName = selectedSnapshot.FileName,
                MapName = selectedSnapshot.MapName,
                UpdatedAt = selectedSnapshot.Map.UpdatedAt
            },
            Prompt = new LlmContextPrompt
            {
                NodeId = promptNode.UniqueIdentifier!.Value,
                Text = promptText,
                RawText = promptNode.Name,
                Path = string.Join(" > ", selectedPath.Select(entry => NormalizeDisplayText(entry.Node))),
                PathSegments = selectedPath.Select(entry => NormalizeDisplayText(entry.Node)).ToArray(),
                TaskState = LlmPromptService.GetTaskStateLabel(promptNode.TaskState)
            },
            Ancestors = selectedPath
                .Take(selectedPath.Count - 1)
                .Select(entry => new LlmContextAncestor
                {
                    NodeId = entry.Node.UniqueIdentifier!.Value,
                    Name = NormalizeDisplayText(entry.Node),
                    NodeType = entry.Node.NodeType,
                    TaskState = LlmPromptService.GetTaskStateLabel(entry.Node.TaskState),
                    Depth = entry.Depth
                })
                .ToArray(),
            Subtree = BuildContextNode(promptNode),
            Links = new LlmContextLinks
            {
                Outgoing = outgoing,
                Backlinks = backlinks
            },
            Urls = CollectUrls(selectedPath.Select(entry => entry.Node), promptNode, outgoing, backlinks)
        };
    }

    private static LlmMapSnapshot BuildSnapshot(LlmJobStore jobStore, string filePath, MindMap map)
    {
        var fileName = Path.GetFileName(filePath);
        return new LlmMapSnapshot(
            jobStore.BuildProtocolMapFilePath(filePath),
            fileName,
            Path.GetFileNameWithoutExtension(fileName),
            map);
    }

    private static IReadOnlyDictionary<Guid, LlmIndexedNode> BuildNodeIndex(IEnumerable<LlmMapSnapshot> snapshots)
    {
        var index = new Dictionary<Guid, LlmIndexedNode>();
        foreach (var snapshot in snapshots)
        {
            foreach (var entry in EnumerateNodes(snapshot.Map.RootNode, depth: 0))
            {
                if (!entry.Node.UniqueIdentifier.HasValue)
                    continue;

                index.TryAdd(entry.Node.UniqueIdentifier.Value, new LlmIndexedNode(snapshot, entry.Node));
            }
        }

        return index;
    }

    private static IReadOnlyList<LlmContextRelatedNode> BuildOutgoingLinks(
        Node node,
        IReadOnlyDictionary<Guid, LlmIndexedNode> nodeIndex)
    {
        return node.Links.Values
            .Select(link =>
            {
                if (!nodeIndex.TryGetValue(link.id, out var target))
                    return null;

                return BuildRelatedNode(target, "outgoing", link.relationType.ToDisplayString());
            })
            .OfType<LlmContextRelatedNode>()
            .OrderBy(entry => entry.MapName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.NodePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.NodeId)
            .ToArray();
    }

    private static IReadOnlyList<LlmContextRelatedNode> BuildBacklinks(
        Node node,
        IReadOnlyDictionary<Guid, LlmIndexedNode> nodeIndex)
    {
        if (!node.UniqueIdentifier.HasValue)
            return Array.Empty<LlmContextRelatedNode>();

        var targetId = node.UniqueIdentifier.Value;
        return nodeIndex.Values
            .Where(entry => entry.Node.Links.ContainsKey(targetId))
            .Select(entry =>
            {
                var link = entry.Node.Links[targetId];
                return BuildRelatedNode(entry, "backlink", $"backlink: {link.relationType.ToDisplayString()}");
            })
            .OrderBy(entry => entry.MapName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.NodePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.NodeId)
            .ToArray();
    }

    private static LlmContextRelatedNode BuildRelatedNode(
        LlmIndexedNode entry,
        string direction,
        string relationLabel)
    {
        var pathSegments = NodeDisplayHelper.BuildNodePathSegments(entry.Node);
        return new LlmContextRelatedNode
        {
            Direction = direction,
            RelationLabel = relationLabel,
            MapPath = entry.Snapshot.FilePath,
            MapName = entry.Snapshot.MapName,
            NodeId = entry.Node.UniqueIdentifier!.Value,
            NodeName = NormalizeDisplayText(entry.Node),
            NodePath = string.Join(" > ", pathSegments),
            NodePathSegments = pathSegments.ToArray()
        };
    }

    private static LlmContextNode BuildContextNode(Node node) =>
        new()
        {
            NodeId = node.UniqueIdentifier!.Value,
            Name = NormalizeDisplayText(node),
            RawText = node.Name,
            NodeType = node.NodeType,
            TaskState = LlmPromptService.GetTaskStateLabel(node.TaskState),
            Links = node.Links.Values
                .OrderBy(link => link.id)
                .Select(link => new LlmContextNodeLink
                {
                    NodeId = link.id,
                    RelationType = link.relationType,
                    Metadata = link.metadata
                })
                .ToArray(),
            Urls = ExtractUrls(node.Name),
            Children = node.Children.Select(BuildContextNode).ToArray()
        };

    private static IReadOnlyList<LlmContextUrl> CollectUrls(
        IEnumerable<Node> ancestors,
        Node subtreeRoot,
        IEnumerable<LlmContextRelatedNode> outgoing,
        IEnumerable<LlmContextRelatedNode> backlinks)
    {
        var urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string url, string source)
        {
            if (!urls.ContainsKey(url))
                urls[url] = source;
        }

        foreach (var ancestor in ancestors)
        {
            foreach (var url in ExtractUrls(ancestor.Name))
                Add(url, $"ancestor:{ancestor.UniqueIdentifier}");
        }

        foreach (var entry in EnumerateNodes(subtreeRoot, depth: 0))
        {
            foreach (var url in ExtractUrls(entry.Node.Name))
                Add(url, $"subtree:{entry.Node.UniqueIdentifier}");
        }

        foreach (var related in outgoing.Concat(backlinks))
        {
            foreach (var url in ExtractUrls(related.NodeName))
                Add(url, $"link:{related.NodeId}");
        }

        return urls
            .Select(entry => new LlmContextUrl { Url = entry.Key, Source = entry.Value })
            .OrderBy(entry => entry.Url, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Source, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractUrls(string? value)
    {
        var text = value ?? string.Empty;
        return UrlRegex.Matches(text)
            .Select(match => match.Value.TrimEnd('.', ',', ';', ':', '!', '?'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<NodePathEntry> FindNodePath(Node rootNode, Guid nodeId)
    {
        var path = new List<NodePathEntry>();
        Visit(rootNode, depth: 0);
        return path;

        bool Visit(Node node, int depth)
        {
            path.Add(new NodePathEntry(node, depth));
            if (node.UniqueIdentifier == nodeId)
                return true;

            foreach (var child in node.Children)
            {
                if (Visit(child, depth + 1))
                    return true;
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }
    }

    private static IEnumerable<NodePathEntry> EnumerateNodes(Node node, int depth)
    {
        yield return new NodePathEntry(node, depth);
        foreach (var child in node.Children)
        {
            foreach (var entry in EnumerateNodes(child, depth + 1))
                yield return entry;
        }
    }

    private static string NormalizeDisplayText(Node node) =>
        NodeDisplayHelper.GetSingleLinePreview(node.Name);

    private static void AppendMarkdownNode(List<string> lines, LlmContextNode node, int depth)
    {
        var indent = new string(' ', depth * 2);
        var marker = node.TaskState == "none" ? "-" : $"- [{node.TaskState}]";
        lines.Add($"{indent}{marker} {node.Name} ({node.NodeId})");

        var rawLines = node.RawText.ReplaceLineEndings("\n").Split('\n');
        foreach (var line in rawLines.Skip(1))
        {
            lines.Add($"{indent}  > {line}");
        }

        foreach (var child in node.Children)
        {
            AppendMarkdownNode(lines, child, depth + 1);
        }
    }

    private sealed record LlmMapSnapshot(string FilePath, string FileName, string MapName, MindMap Map);

    private sealed record LlmIndexedNode(LlmMapSnapshot Snapshot, Node Node);

    private sealed record NodePathEntry(Node Node, int Depth);
}

internal sealed class LlmContextDocument
{
    public int Version { get; set; } = 1;

    public string Mode { get; set; } = LlmPromptService.ContextMode;

    public DateTimeOffset GeneratedAt { get; set; }

    public LlmContextMap Map { get; set; } = new();

    public LlmContextPrompt Prompt { get; set; } = new();

    public IReadOnlyList<LlmContextAncestor> Ancestors { get; set; } = Array.Empty<LlmContextAncestor>();

    public LlmContextNode Subtree { get; set; } = new();

    public LlmContextLinks Links { get; set; } = new();

    public IReadOnlyList<LlmContextUrl> Urls { get; set; } = Array.Empty<LlmContextUrl>();
}

internal sealed class LlmContextMap
{
    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string MapName { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }
}

internal sealed class LlmContextPrompt
{
    public Guid NodeId { get; set; }

    public string Text { get; set; } = string.Empty;

    public string RawText { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public IReadOnlyList<string> PathSegments { get; set; } = Array.Empty<string>();

    public string TaskState { get; set; } = "none";
}

internal sealed class LlmContextAncestor
{
    public Guid NodeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public NodeType NodeType { get; set; }

    public string TaskState { get; set; } = "none";

    public int Depth { get; set; }
}

internal sealed class LlmContextNode
{
    public Guid NodeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string RawText { get; set; } = string.Empty;

    public NodeType NodeType { get; set; }

    public string TaskState { get; set; } = "none";

    public IReadOnlyList<LlmContextNodeLink> Links { get; set; } = Array.Empty<LlmContextNodeLink>();

    public IReadOnlyList<string> Urls { get; set; } = Array.Empty<string>();

    public IReadOnlyList<LlmContextNode> Children { get; set; } = Array.Empty<LlmContextNode>();
}

internal sealed class LlmContextNodeLink
{
    public Guid NodeId { get; set; }

    public LinkRelationType RelationType { get; set; }

    public string? Metadata { get; set; }
}

internal sealed class LlmContextLinks
{
    public IReadOnlyList<LlmContextRelatedNode> Outgoing { get; set; } = Array.Empty<LlmContextRelatedNode>();

    public IReadOnlyList<LlmContextRelatedNode> Backlinks { get; set; } = Array.Empty<LlmContextRelatedNode>();
}

internal sealed class LlmContextRelatedNode
{
    public string Direction { get; set; } = string.Empty;

    public string RelationLabel { get; set; } = string.Empty;

    public string MapPath { get; set; } = string.Empty;

    public string MapName { get; set; } = string.Empty;

    public Guid NodeId { get; set; }

    public string NodeName { get; set; } = string.Empty;

    public string NodePath { get; set; } = string.Empty;

    public IReadOnlyList<string> NodePathSegments { get; set; } = Array.Empty<string>();
}

internal sealed class LlmContextUrl
{
    public string Url { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}
