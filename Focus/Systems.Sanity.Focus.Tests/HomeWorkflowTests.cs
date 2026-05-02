using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;
using System.Collections.Generic;

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
    public void Execute_UppercaseExitCommand_ReturnsExitResult()
    {
        using var workspace = new TestWorkspace();
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("EXIT"), workflow.GetFileSelection());

        Assert.True(result.ShouldExit);
        Assert.False(result.IsError);
    }

    [Fact]
    public void Execute_CreateFile_OpensCreateMapWithRequestedName()
    {
        var navigator = new RecordingPageNavigator();
        using var workspace = new TestWorkspace(navigator);
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("new alpha"), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.Equal("alpha", navigator.OpenedCreateMapFileName);
        Assert.NotNull(navigator.OpenedCreateMapMindMap);
        Assert.Equal("alpha", navigator.OpenedCreateMapMindMap!.RootNode.Name);
    }

    [Fact]
    public void Execute_RefreshCommand_ReturnsContinueWithoutOpeningMap()
    {
        var navigator = new RecordingPageNavigator();
        using var workspace = new TestWorkspace(navigator);
        workspace.SaveMap("alpha", new MindMap("Alpha"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("ls"), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.Null(navigator.OpenedEditMapFilePath);
    }

    [Fact]
    public void Execute_MapNamedLs_RefreshTakesPrecedenceAndMapStillOpensByIdentifier()
    {
        static (HomeWorkflowResult Result, string FilePath, RecordingPageNavigator Navigator) ExecuteAgainstLsMap(string input)
        {
            var navigator = new RecordingPageNavigator();
            using var workspace = new TestWorkspace(navigator);
            var filePath = workspace.SaveMap("ls", new MindMap("LS map"));
            var workflow = new HomeWorkflow(workspace.AppContext);

            return (workflow.Execute(new ConsoleInput(input), workflow.GetFileSelection()), filePath, navigator);
        }

        var refreshRun = ExecuteAgainstLsMap("ls");
        var numberRun = ExecuteAgainstLsMap("1");
        var shortcutRun = ExecuteAgainstLsMap(AccessibleKeyNumbering.GetStringFor(1));

        Assert.False(refreshRun.Result.ShouldExit);
        Assert.False(refreshRun.Result.IsError);
        Assert.Null(refreshRun.Navigator.OpenedEditMapFilePath);

        Assert.False(numberRun.Result.ShouldExit);
        Assert.False(numberRun.Result.IsError);
        Assert.Equal(numberRun.FilePath, numberRun.Navigator.OpenedEditMapFilePath);

        Assert.False(shortcutRun.Result.ShouldExit);
        Assert.False(shortcutRun.Result.IsError);
        Assert.Equal(shortcutRun.FilePath, shortcutRun.Navigator.OpenedEditMapFilePath);
    }

    [Fact]
    public void Execute_LocalizedShortcut_OpensSelectedMap()
    {
        var navigator = new RecordingPageNavigator();
        using var workspace = new TestWorkspace(navigator);
        using var translationScope = TranslationTestScope.UseGeorgian();
        var filePath = workspace.SaveMap("alpha", new MindMap("Alpha"));
        var workflow = new HomeWorkflow(workspace.AppContext);
        var localizedShortcut = AccessibleKeyNumbering.GetStringFor(1).ToLocalLanguage();

        var result = workflow.Execute(new ConsoleInput(localizedShortcut), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.Equal(filePath, navigator.OpenedEditMapFilePath);
    }

    [Fact]
    public void Execute_LocalizedFileName_OpensSelectedMap()
    {
        var navigator = new RecordingPageNavigator();
        using var workspace = new TestWorkspace(navigator);
        using var translationScope = TranslationTestScope.UseGeorgian();
        var filePath = workspace.SaveMap("alpha", new MindMap("Alpha"));
        var workflow = new HomeWorkflow(workspace.AppContext);
        var localizedFileName = "alpha".ToLocalLanguage();

        var result = workflow.Execute(new ConsoleInput(localizedFileName), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.Equal(filePath, navigator.OpenedEditMapFilePath);
    }

    [Fact]
    public void Execute_CapsLockedLocalizedFileIdentifier_OpensSelectedMap()
    {
        var navigator = new RecordingPageNavigator();
        using var workspace = new TestWorkspace(navigator);
        using var translationScope = new TranslationTestScope(
            TranslationTestScope.CreateTranslation("caps-home", new Dictionary<string, string>
            {
                ["ä"] = "a",
                ["ł"] = "l",
                ["þ"] = "p",
                ["ħ"] = "h",
                ["ĵ"] = "j"
            }));
        var filePath = workspace.SaveMap("alpha", new MindMap("Alpha"));
        var workflow = new HomeWorkflow(workspace.AppContext);
        var localizedShortcut = AccessibleKeyNumbering.GetStringFor(1).ToLocalLanguage().ToUpperInvariant();

        var result = workflow.Execute(new ConsoleInput(localizedShortcut), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.Equal(filePath, navigator.OpenedEditMapFilePath);
    }

    [Fact]
    public void Execute_WhenOpeningMapThrows_ReturnsGenericErrorAndLogsException()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        var navigator = new RecordingPageNavigator
        {
            OpenEditMapException = new InvalidOperationException("navigator failed")
        };
        using var workspace = new TestWorkspace(navigator);
        workspace.SaveMap("alpha", new MindMap("Alpha"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("1"), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.True(result.IsError);
        Assert.Equal(ExceptionDiagnostics.BuildUserMessage("opening map"), result.Message);
        Assert.Contains("Action: opening map", diagnosticsScope.ReadLog());
        Assert.Contains("navigator failed", diagnosticsScope.ReadLog());
    }

    [Fact]
    public void Execute_WhenOpeningMapHasAutoResolveConflict_ReturnsSpecificConflictMessage()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        var navigator = new RecordingPageNavigator
        {
            OpenEditMapException = new MapConflictAutoResolveException()
        };
        using var workspace = new TestWorkspace(navigator);
        workspace.SaveMap("alpha", new MindMap("Alpha"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("1"), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.True(result.IsError);
        Assert.Equal(MapConflictAutoResolveException.DefaultMessage, result.Message);
        Assert.DoesNotContain("Action: opening map", diagnosticsScope.ReadLog(), StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_DeleteCommand_DeletesMapAndNodeScopedAttachmentsAfterConfirmation()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Alpha");
        var filePath = workspace.SaveMap("alpha", map);
        var attachmentDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(
            filePath,
            map.RootNode.UniqueIdentifier ?? throw new InvalidOperationException("Root node identifier is required."));
        Directory.CreateDirectory(attachmentDirectory);
        File.WriteAllText(Path.Combine(attachmentDirectory, "capture.png"), "attachment");
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("yes"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("del 1"), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.False(File.Exists(filePath));
        Assert.False(Directory.Exists(attachmentDirectory));
    }

    [Fact]
    public void Execute_DeleteCommand_WhenMapIsUnreadable_ReturnsSpecificMessageWithoutLogging()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        using var workspace = new TestWorkspace();
        var filePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "broken.json");
        File.WriteAllText(filePath, "{ \"rootNode\": ");
        var attachmentDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(filePath, Guid.NewGuid());
        Directory.CreateDirectory(attachmentDirectory);
        File.WriteAllText(Path.Combine(attachmentDirectory, "capture.png"), "attachment");
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("yes"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("del 1"), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.True(result.IsError);
        Assert.Equal(
            "Map \"broken.json\" cannot be deleted because it is unreadable and its attachment folders cannot be determined safely.",
            result.Message);
        Assert.True(File.Exists(filePath));
        Assert.True(Directory.Exists(attachmentDirectory));
        Assert.DoesNotContain("Action: deleting map", diagnosticsScope.ReadLog(), StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_DeleteCommand_UsesWorkflowInteractionConfirmation()
    {
        var interactions = new RecordingWorkflowInteractions
        {
            DefaultConfirmationResult = false
        };
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        var filePath = workspace.SaveMap("alpha", new MindMap("Alpha"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("del 1"), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.True(File.Exists(filePath));
        Assert.Equal(
            ["Are you sure you want to delete: \"alpha.json\" and all of its attachments?"],
            interactions.ConfirmationMessages);
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
    public void Execute_Search_UsesWorkflowInteractionResultSelection()
    {
        var navigator = new RecordingPageNavigator();
        var interactions = new RecordingWorkflowInteractions
        {
            SearchResultSelector = results => results.Single()
        };
        using var workspace = new TestWorkspace(navigator, workflowInteractions: interactions);

        var map = new MindMap("Search Root");
        var child = map.AddAtCurrentNode("Unique Search Result");
        var filePath = workspace.SaveMap("search-map", map);
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("search Unique"), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.Equal(filePath, navigator.OpenedEditMapFilePath);
        Assert.Equal(child.UniqueIdentifier, navigator.OpenedEditMapNodeIdentifier);
        Assert.Equal(["Search results for \"Unique\""], interactions.SearchSelectionTitles);
        Assert.True(interactions.SearchSelectionDisplayOptions.Single().IncludeMapName);
    }

    [Fact]
    public void Execute_RenameCommand_UsesWorkflowInteractionReturnedPath()
    {
        var interactions = new RecordingWorkflowInteractions
        {
            RenameMapFileSelector = (repository, file) =>
            {
                var newPath = Path.Combine(file.DirectoryName!, "beta.json");
                repository.MoveMap(file.FullName, newPath);
                return newPath;
            }
        };
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        var originalPath = workspace.SaveMap("alpha", new MindMap("Alpha"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("ren 1"), workflow.GetFileSelection());
        var renamedPath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "beta.json");
        var renamedMap = workspace.MapsStorage.OpenMap(renamedPath);

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.Equal(1, interactions.RenameMapFileCallCount);
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(renamedPath));
        Assert.Equal("beta", renamedMap.RootNode.Name);
    }

    [Fact]
    public void Execute_UppercaseTasksCommandAndFilter_OpensSelectedMapAndNode()
    {
        var navigator = new RecordingPageNavigator();
        using var workspace = new TestWorkspace(navigator);

        var map = new MindMap("Alpha");
        map.AddAtCurrentNode("Doing task");
        Assert.True(map.SetTaskState("1", TaskState.Doing, out _));
        var filePath = workspace.SaveMap("alpha", map);
        var taskId = map.GetNode("1")!.UniqueIdentifier;

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("1"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("TASKS DOING"), workflow.GetFileSelection());

        Assert.False(result.ShouldExit);
        Assert.False(result.IsError);
        Assert.Equal(filePath, navigator.OpenedEditMapFilePath);
        Assert.Equal(taskId, navigator.OpenedEditMapNodeIdentifier);
    }

    [Fact]
    public void BuildHomePageText_UsesGroupedHelpWhileKeepingFileList()
    {
        using var workspace = new TestWorkspace();
        workspace.SaveMap("alpha", new MindMap("Alpha"));
        var workflow = new HomeWorkflow(workspace.AppContext);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession(80));
        var text = workflow.BuildHomePageText(workflow.GetFileSelection(), showCommands: true);

        Assert.Contains("- alpha.", text, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains(ColorLabel("Create"), text);
        Assert.Contains($"{ColorLabel("Open")}1, ja", text);
        Assert.Contains(ColorLabel("Manage"), text);
        Assert.Contains(ColorLabel("Find"), text);
        Assert.Contains(ColorLabel("System"), text);
        Assert.DoesNotContain("Type file identifier", text, StringComparison.InvariantCulture);
        Assert.True(
            text.IndexOf(ColorLabel("Open"), StringComparison.InvariantCulture) <
            text.IndexOf(ColorLabel("Manage"), StringComparison.InvariantCulture));
    }

    private static string ColorLabel(string label) =>
        $"[{ConfigurationConstants.CommandColor}]{label}[!]: ";
}
