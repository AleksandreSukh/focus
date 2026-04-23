using System.Collections.Generic;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class NodeBranchVisibility
{
    public static bool HasHideDoneAncestor(Node node)
    {
        var lineage = new Stack<Node>();
        var parentNode = node.GetParent();
        while (parentNode != null)
        {
            lineage.Push(parentNode);
            parentNode = parentNode.GetParent();
        }

        var hideDoneState = false;
        while (lineage.Count > 0)
        {
            hideDoneState = HideDoneStateForChildren(lineage.Pop(), hideDoneState);
        }

        return hideDoneState;
    }

    public static bool HideDoneStateForNode(Node node) =>
        HideDoneStateForChildren(node, HasHideDoneAncestor(node));

    public static bool HideDoneStateForChildren(Node node, bool ancestorHidesDone) =>
        node.GetHideDoneTasksOverride() ?? ancestorHidesDone;

    public static bool ShouldHideNode(Node node, bool ancestorHidesDone) =>
        ancestorHidesDone && node.TaskState == TaskState.Done;
}
