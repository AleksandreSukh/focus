using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal sealed record NodeExportOptions(
    bool SkipCollapsedDescendants = false,
    bool UseBlackBackground = false);

internal static class NodeExportHelpers
{
    private const string UntitledNodeName = "Untitled";

    public static IEnumerable<Node> GetVisibleChildren(Node node) =>
        node.Children.Where(child => child.NodeType != NodeType.IdeaBagItem);

    public static string NormalizeNodeName(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
            return UntitledNodeName;

        return nodeName.ReplaceLineEndings(" ").Trim();
    }

    public static string FormatNodeName(Node node) =>
        node.TaskState.WithDisplayMarker(NormalizeNodeName(node.Name));
}
