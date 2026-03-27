using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Tests;

public class EditWorkflowTests
{
    [Fact]
    public void Execute_GoToChild_UpdatesCurrentNode()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("cd 1"));

        Assert.True(result.IsSuccess);
        Assert.Contains("Child", workflow.BuildScreen());
    }

    [Fact]
    public void Execute_HideAndUnhideNode_RequestPersistence()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var hideResult = workflow.Execute(new ConsoleInput("min 1"));
        var unhideResult = workflow.Execute(new ConsoleInput("max 1"));

        Assert.True(hideResult.IsSuccess);
        Assert.True(hideResult.ShouldPersist);
        Assert.True(unhideResult.IsSuccess);
        Assert.True(unhideResult.ShouldPersist);
    }

    [Fact]
    public void Execute_LinkFrom_QueuesLinkSource()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("First");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("linkfrom 1"));

        Assert.True(result.IsSuccess);
        Assert.True(workspace.AppContext.LinkIndex.HasQueuedLinkSources);
        Assert.Contains("First", workflow.BuildScreen());
    }

    [Fact]
    public void Execute_TodoAndNotask_OnCurrentNodeUpdatesSavedMap()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        Assert.True(workflow.Execute(new ConsoleInput("cd 1")).IsSuccess);

        var todoResult = workflow.Execute(new ConsoleInput("todo"));
        workflow.Save();

        var todoMap = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(todoResult.IsSuccess);
        Assert.True(todoResult.ShouldPersist);
        Assert.Equal(TaskState.Todo, todoMap.GetNode("1")!.TaskState);

        var clearWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        Assert.True(clearWorkflow.Execute(new ConsoleInput("cd 1")).IsSuccess);

        var clearResult = clearWorkflow.Execute(new ConsoleInput("notask"));
        clearWorkflow.Save();

        var clearedMap = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(clearResult.IsSuccess);
        Assert.True(clearResult.ShouldPersist);
        Assert.Equal(TaskState.None, clearedMap.GetNode("1")!.TaskState);
    }

    [Fact]
    public void Execute_DoneAndToggle_OnAddressedChildFollowTaskTransitions()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var doneResult = workflow.Execute(new ConsoleInput("dn 1"));
        workflow.Save();
        var doneMap = workspace.MapsStorage.OpenMap(filePath);

        var toggleWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        var toggleResult = toggleWorkflow.Execute(new ConsoleInput("tg 1"));
        toggleWorkflow.Save();
        var toggledMap = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(doneResult.IsSuccess);
        Assert.True(doneResult.ShouldPersist);
        Assert.Equal(TaskState.Done, doneMap.GetNode("1")!.TaskState);
        Assert.True(toggleResult.IsSuccess);
        Assert.True(toggleResult.ShouldPersist);
        Assert.Equal(TaskState.Todo, toggledMap.GetNode("1")!.TaskState);
    }

    [Fact]
    public void Execute_TodoAtRoot_ReturnsValidationError()
    {
        using var workspace = new TestWorkspace();
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("todo"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Can't change task state for root node", result.ErrorString);
    }

    [Fact]
    public void Execute_Tasks_DefaultsToOpenTasksAndOpensDoingTaskFirst()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Todo task");
        map.AddAtCurrentNode("Doing task");
        map.AddAtCurrentNode("Done task");
        Assert.True(map.SetTaskState("1", TaskState.Todo, out _));
        Assert.True(map.SetTaskState("2", TaskState.Doing, out _));
        Assert.True(map.SetTaskState("3", TaskState.Done, out _));
        var filePath = workspace.SaveMap("workflow-map", map);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("1"));
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("tasks"));

        Assert.True(result.IsSuccess);
        Assert.Contains("[~] Doing task", workflow.BuildScreen());
    }

    [Fact]
    public void Execute_Tasks_WithDoneFilterOpensDoneTask()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Todo task");
        map.AddAtCurrentNode("Done task");
        Assert.True(map.SetTaskState("1", TaskState.Todo, out _));
        Assert.True(map.SetTaskState("2", TaskState.Done, out _));
        var filePath = workspace.SaveMap("workflow-map", map);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("1"));
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("ts done"));

        Assert.True(result.IsSuccess);
        Assert.Contains("[x] Done task", workflow.BuildScreen());
    }
}
