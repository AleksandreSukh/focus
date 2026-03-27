using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus;
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

    [Fact]
    public void Execute_Tasks_OpensSelectedMapAndNode()
    {
        var navigator = new RecordingPageNavigator();
        using var workspace = new TestWorkspace(navigator);

        var alphaMap = new MindMap("Alpha");
        alphaMap.AddAtCurrentNode("Todo task");
        Assert.True(alphaMap.SetTaskState("1", TaskState.Todo, out _));
        workspace.SaveMap("alpha", alphaMap);

        var betaMap = new MindMap("Beta");
        betaMap.AddAtCurrentNode("Doing task");
        Assert.True(betaMap.SetTaskState("1", TaskState.Doing, out _));
        var betaFilePath = workspace.SaveMap("beta", betaMap);
        var betaTaskId = betaMap.GetNode("1")!.UniqueIdentifier;

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("1"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("tasks doing"), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.Equal(betaFilePath, navigator.OpenedEditMapFilePath);
        Assert.Equal(betaTaskId, navigator.OpenedEditMapNodeIdentifier);
    }

    [Fact]
    public void BuildHomePageText_UsesGroupedHelpWhileKeepingFileList()
    {
        using var workspace = new TestWorkspace();
        workspace.SaveMap("alpha", new MindMap("Alpha"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession(80));
        var text = workflow.BuildHomePageText(workflow.GetFileSelection());

        Assert.Contains("- alpha.", text, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains(ColorLabel("Create"), text);
        Assert.Contains(ColorLabel("Manage"), text);
        Assert.Contains(ColorLabel("Find"), text);
        Assert.Contains(ColorLabel("System"), text);
    }

    private static string ColorLabel(string label) =>
        $"[{ConfigurationConstants.CommandColor}]{label}[!]: ";
}
