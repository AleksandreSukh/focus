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

    [Fact]
    public void ToggleTaskState_FollowsExpectedTransitions()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Task");
        Assert.True(map.ChangeCurrentNode("1"));

        Assert.True(map.ToggleTaskState(out var firstToggleError));
        Assert.Equal(string.Empty, firstToggleError);
        Assert.Equal(TaskState.Todo, map.GetCurrentNode().TaskState);

        Assert.True(map.ToggleTaskState(out _));
        Assert.Equal(TaskState.Done, map.GetCurrentNode().TaskState);

        Assert.True(map.SetTaskState(TaskState.Doing, out _));
        Assert.Equal(TaskState.Doing, map.GetCurrentNode().TaskState);

        Assert.True(map.ToggleTaskState(out _));
        Assert.Equal(TaskState.Done, map.GetCurrentNode().TaskState);
    }

    [Fact]
    public void SetTaskState_RejectsRootAndIdeaTags()
    {
        var map = new MindMap("Root");

        var rootResult = map.SetTaskState(TaskState.Todo, out var rootError);

        map.AddIdeaAtCurrentNode("Idea");
        var ideaResult = map.SetTaskState("1", TaskState.Todo, out var ideaError);

        Assert.False(rootResult);
        Assert.Equal("Can't change task state for root node", rootError);
        Assert.False(ideaResult);
        Assert.Equal("Task mode is not supported for idea tags", ideaError);
    }

    [Fact]
    public void DetachCurrentNodeAsNewMap_PreservesTaskState()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        Assert.True(map.SetTaskState("1", TaskState.Todo, out _));
        Assert.True(map.ChangeCurrentNode("1"));

        var detached = map.DetachCurrentNodeAsNewMap();

        Assert.NotNull(detached);
        Assert.Equal(TaskState.Todo, detached!.RootNode.TaskState);
    }
}
