using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class NodeBranchVisibility
{
    public static bool HasHideDoneAncestor(Node node)
    {
        var parentNode = node.GetParent();
        while (parentNode != null)
        {
            if (parentNode.HideDoneTasks)
                return true;

            parentNode = parentNode.GetParent();
        }

        return false;
    }

    public static bool HideDoneStateForChildren(Node node, bool ancestorHidesDone) =>
        ancestorHidesDone || node.HideDoneTasks;

    public static bool ShouldHideNode(Node node, bool ancestorHidesDone) =>
        ancestorHidesDone && node.TaskState == TaskState.Done;
}
