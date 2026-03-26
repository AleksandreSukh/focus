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
}
