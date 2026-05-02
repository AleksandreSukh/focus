using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Input;
using System.Threading;

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
    public void GetChildren_UsesSharedSelectorsForTextAndBlockNodes_AndSkipsIdeaTags()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Task");
        map.AddIdeaAtCurrentNode("Idea");
        map.AddBlockAtCurrentNode("Block title\nBlock body");

        var children = map.GetChildren();

        Assert.Equal(2, children.Count);
        Assert.Equal("Task", children[1]);
        Assert.Equal("Block title", children[2]);
        Assert.False(map.HasNode("Idea"));
        Assert.True(map.HasNode("2"));
    }

    [Fact]
    public void GetChildren_WhenAncestorHidesDoneTasks_SkipsDoneChildrenAndTheirShortcuts()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Branch");
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddAtCurrentNode("Done child");
        map.AddAtCurrentNode("Open child");
        Assert.True(map.SetTaskState("1", TaskState.Done, out _));
        Assert.True(map.SetTaskState("2", TaskState.Todo, out _));
        map.GoToRoot();
        Assert.True(map.SetHideDoneTasks(true, out _));
        Assert.True(map.ChangeCurrentNode("1"));

        var children = map.GetChildren();

        Assert.Single(children);
        Assert.False(children.ContainsKey(1));
        Assert.Equal("Open child", children[2]);
        Assert.False(map.HasNode("1"));
        Assert.False(map.HasNode(AccessibleKeyNumbering.GetStringFor(1)));
        Assert.True(map.HasNode("2"));
        Assert.True(map.HasNode(AccessibleKeyNumbering.GetStringFor(2)));
    }

    [Fact]
    public void GetChildren_WhenCurrentNodeShowsDoneOverride_IncludesDoneChildren()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Branch");
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddAtCurrentNode("Done child");
        Assert.True(map.SetTaskState("1", TaskState.Done, out _));
        map.GoToRoot();
        Assert.True(map.SetHideDoneTasks(true, out _));
        Assert.True(map.SetHideDoneTasks("1", false, out _));
        Assert.True(map.ChangeCurrentNode("1"));

        var children = map.GetChildren();

        Assert.Single(children);
        Assert.Equal("Done child", children[1]);
        Assert.True(map.HasNode("1"));
        Assert.True(map.HasNode(AccessibleKeyNumbering.GetStringFor(1)));
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
    public void SetHideDoneTasks_AllowsRootAndRejectsIdeaTags()
    {
        var map = new MindMap("Root");

        var rootResult = map.SetHideDoneTasks(true, out var rootError);

        map.AddIdeaAtCurrentNode("Idea");
        var ideaResult = map.SetHideDoneTasks("1", true, out var ideaError);

        Assert.True(rootResult);
        Assert.Equal(string.Empty, rootError);
        Assert.True(map.RootNode.HideDoneTasks);
        Assert.True(map.RootNode.HideDoneTasksExplicit);
        Assert.False(ideaResult);
        Assert.Equal("Hide done tasks is not supported for idea tags", ideaError);
    }

    [Fact]
    public void SetHideDoneTasks_ParentRefreshClearsChildOverride()
    {
        var map = new MindMap("Root");
        var branch = map.AddAtCurrentNode("Branch");

        Assert.True(map.SetHideDoneTasks(true, out _));
        Assert.True(map.SetHideDoneTasks("1", false, out _));

        Assert.False(branch.HideDoneTasks);
        Assert.True(branch.HideDoneTasksExplicit);

        Assert.True(map.SetHideDoneTasks(true, out _));

        Assert.True(map.RootNode.HideDoneTasks);
        Assert.True(map.RootNode.HideDoneTasksExplicit);
        Assert.False(branch.HideDoneTasks);
        Assert.Null(branch.HideDoneTasksExplicit);
    }

    [Fact]
    public void SetHideDoneTasks_ParentRefreshClearsChildOverrideAndRestoresInheritedVisibility()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Branch");
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddAtCurrentNode("Open child");
        map.AddAtCurrentNode("Done child");
        Assert.True(map.SetTaskState("1", TaskState.Todo, out _));
        Assert.True(map.SetTaskState("2", TaskState.Done, out _));

        map.GoToRoot();
        Assert.True(map.SetHideDoneTasks(true, out _));
        Assert.True(map.SetHideDoneTasks("1", false, out _));
        Assert.True(map.ChangeCurrentNode("1"));
        Assert.Equal(2, map.GetChildren().Count);

        map.GoToRoot();
        Assert.True(map.SetHideDoneTasks(true, out _));
        Assert.True(map.ChangeCurrentNode("1"));
        var children = map.GetChildren();

        Assert.Single(children);
        Assert.Equal("Open child", children[1]);
        Assert.False(children.ContainsValue("Done child"));
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

    [Fact]
    public void Mutations_UpdateNodeMetadataTimestamps()
    {
        var map = new MindMap("Root");
        var child = map.AddAtCurrentNode("Child");
        var initialUpdatedAt = child.Metadata!.UpdatedAtUtc;

        PauseForTimestampResolution();
        Assert.True(map.ChangeCurrentNode("1"));
        map.EditCurrentNode("Child updated");
        var editedUpdatedAt = map.GetCurrentNode().Metadata!.UpdatedAtUtc;

        PauseForTimestampResolution();
        Assert.True(map.SetTaskState(TaskState.Todo, out _));
        var taskUpdatedAt = map.GetCurrentNode().Metadata!.UpdatedAtUtc;

        PauseForTimestampResolution();
        Assert.True(map.SetHideDoneTasks(true, out _));
        var hideDoneUpdatedAt = map.GetCurrentNode().Metadata!.UpdatedAtUtc;

        Assert.True(editedUpdatedAt > initialUpdatedAt);
        Assert.True(taskUpdatedAt > editedUpdatedAt);
        Assert.True(hideDoneUpdatedAt > taskUpdatedAt);
    }

    [Fact]
    public void ChildListMutations_UpdateParentMetadataTimestamp()
    {
        var map = new MindMap("Root");
        var rootUpdatedAt = map.RootNode.Metadata!.UpdatedAtUtc;

        PauseForTimestampResolution();
        map.AddAtCurrentNode("Child");
        var afterAddUpdatedAt = map.RootNode.Metadata!.UpdatedAtUtc;

        PauseForTimestampResolution();
        Assert.True(map.DeleteChildNode("1"));
        var afterDeleteUpdatedAt = map.RootNode.Metadata!.UpdatedAtUtc;

        Assert.True(afterAddUpdatedAt > rootUpdatedAt);
        Assert.True(afterDeleteUpdatedAt > afterAddUpdatedAt);
    }

    private static void PauseForTimestampResolution()
    {
        Thread.Sleep(20);
    }
}
