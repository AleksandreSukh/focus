using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Tests;

public class HomeWorkflowTests
{
    [Fact]
    public void ResolveFile_UsesSharedMapSelectionRules()
    {
        using var workspace = new TestWorkspace();
        workspace.SaveMap("alpha", new MindMap("Alpha"));

        var workflow = new HomeWorkflow(workspace.AppContext);
        var selection = workflow.GetFileSelection();

        var byNumber = workflow.ResolveFile(selection, "1");
        var byShortcut = workflow.ResolveFile(selection, AccessibleKeyNumbering.GetStringFor(1));
        var byName = workflow.ResolveFile(selection, "alpha");

        Assert.NotNull(byNumber);
        Assert.Equal(byNumber.FullName, byShortcut!.FullName);
        Assert.Equal(byNumber.FullName, byName!.FullName);
    }

    [Fact]
    public void Execute_ExitCommand_ReturnsExitResult()
    {
        using var workspace = new TestWorkspace();
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("exit"), workflow.GetFileSelection());

        Assert.True(result.ShouldExit);
        Assert.False(result.IsError);
    }
}
