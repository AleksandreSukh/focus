using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Tests;

public class MindMapTests
{
    [Fact]
    public void HideAndUnhideNode_TogglesCollapsedState()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");

        var hidden = map.HideNode("1");
        var child = map.GetNode("1");

        Assert.True(hidden);
        Assert.NotNull(child);
        Assert.True(child.IsCollapsed());

        var unhidden = map.UnhideNode("1");

        Assert.True(unhidden);
        Assert.False(child.IsCollapsed());
    }

    [Fact]
    public void DeleteNodeIdeaTags_RemovesOnlyIdeaTagChildren()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Task");
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddIdeaAtCurrentNode("Idea A");
        map.AddIdeaAtCurrentNode("Idea B");
        map.AddAtCurrentNode("Nested task");

        var removed = map.DeleteCurrentNodeIdeaTags();
        var remainingChildren = map.GetChildren();

        Assert.True(removed);
        Assert.Single(remainingChildren);
        Assert.Equal("Nested task", remainingChildren[1]);
    }

    [Fact]
    public void DetachCurrentNodeAsNewMap_ReturnsDetachedMapAndMovesCurrentNodeBackToParent()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        map.AddAtCurrentNode("Sibling");
        Assert.True(map.ChangeCurrentNode("1"));

        var detached = map.DetachCurrentNodeAsNewMap();
        map.GoToRoot();

        Assert.NotNull(detached);
        Assert.Equal("Child", detached!.RootNode.Name);
        Assert.Equal("Root", map.GetCurrentNodeName());
        Assert.Single(map.GetChildren());
        Assert.Equal("Sibling", map.GetChildren()[1]);
    }
}
