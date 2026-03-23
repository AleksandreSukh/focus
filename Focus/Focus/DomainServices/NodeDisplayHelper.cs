using System.Collections.Generic;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class NodeDisplayHelper
{
    public static string BuildNodePath(Node node)
    {
        var pathSegments = new Stack<string>();
        var currentNode = node;
        while (currentNode != null)
        {
            if (!string.IsNullOrWhiteSpace(currentNode.Name))
            {
                pathSegments.Push(currentNode.Name);
            }

            currentNode = currentNode.GetParent();
        }

        return string.Join(" > ", pathSegments);
    }

    public static int GetDepth(Node node)
    {
        var depth = 0;
        var currentNode = node.GetParent();
        while (currentNode != null)
        {
            depth++;
            currentNode = currentNode.GetParent();
        }

        return depth;
    }
}
