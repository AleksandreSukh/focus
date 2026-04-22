using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;

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
        Assert.Equal("Hide node in workflow-map", hideResult.SyncCommitMessage);
        Assert.True(unhideResult.IsSuccess);
        Assert.True(unhideResult.ShouldPersist);
        Assert.Equal("Unhide node in workflow-map", unhideResult.SyncCommitMessage);
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
    public void Execute_AttachmentShortcut_PrefersChildNavigationWhenShortcutMatchesChild()
    {
        var fileOpener = new RecordingFileOpener();
        using var workspace = new TestWorkspace(fileOpener: fileOpener);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var filePath = workspace.SaveMap("workflow-map", map);
        var attachmentPath = workspace.AppContext.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            filePath,
            GetRequiredNodeIdentifier(map.RootNode),
            "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(attachmentPath)!);
        File.WriteAllText(attachmentPath, "attachment");

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("1"));

        Assert.True(result.IsSuccess);
        Assert.Null(fileOpener.OpenedFilePath);
        Assert.Contains("Child", workflow.BuildScreen());
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
        workflow.Save(todoResult.SyncCommitMessage!);

        var todoMap = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(todoResult.IsSuccess);
        Assert.True(todoResult.ShouldPersist);
        Assert.Equal("Mark task as todo in workflow-map", todoResult.SyncCommitMessage);
        Assert.Equal(TaskState.Todo, todoMap.GetNode("1")!.TaskState);

        var clearWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        Assert.True(clearWorkflow.Execute(new ConsoleInput("cd 1")).IsSuccess);

        var clearResult = clearWorkflow.Execute(new ConsoleInput("notask"));
        clearWorkflow.Save(clearResult.SyncCommitMessage!);

        var clearedMap = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(clearResult.IsSuccess);
        Assert.True(clearResult.ShouldPersist);
        Assert.Equal("Clear task state in workflow-map", clearResult.SyncCommitMessage);
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
        workflow.Save(doneResult.SyncCommitMessage!);
        var doneMap = workspace.MapsStorage.OpenMap(filePath);

        var toggleWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        var toggleResult = toggleWorkflow.Execute(new ConsoleInput("tg 1"));
        toggleWorkflow.Save(toggleResult.SyncCommitMessage!);
        var toggledMap = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(doneResult.IsSuccess);
        Assert.True(doneResult.ShouldPersist);
        Assert.Equal("Mark task as done in workflow-map", doneResult.SyncCommitMessage);
        Assert.Equal(TaskState.Done, doneMap.GetNode("1")!.TaskState);
        Assert.True(toggleResult.IsSuccess);
        Assert.True(toggleResult.ShouldPersist);
        Assert.Equal("Toggle task state in workflow-map", toggleResult.SyncCommitMessage);
        Assert.Equal(TaskState.Todo, toggledMap.GetNode("1")!.TaskState);
    }

    [Fact]
    public void Execute_HideDoneAndShowDone_PersistNodeFlag()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Branch");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var hideResult = workflow.Execute(new ConsoleInput("hidedone 1"));
        workflow.Save(hideResult.SyncCommitMessage!);
        var hiddenMap = workspace.MapsStorage.OpenMap(filePath);

        var showWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        Assert.True(showWorkflow.Execute(new ConsoleInput("cd 1")).IsSuccess);
        var showResult = showWorkflow.Execute(new ConsoleInput("showdone"));
        showWorkflow.Save(showResult.SyncCommitMessage!);
        var shownMap = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(hideResult.IsSuccess);
        Assert.True(hideResult.ShouldPersist);
        Assert.Equal("Hide done tasks in workflow-map", hideResult.SyncCommitMessage);
        Assert.True(hiddenMap.GetNode("1")!.HideDoneTasks);
        Assert.True(showResult.IsSuccess);
        Assert.True(showResult.ShouldPersist);
        Assert.Equal("Show done tasks in workflow-map", showResult.SyncCommitMessage);
        Assert.False(shownMap.GetNode("1")!.HideDoneTasks);
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

    [Fact]
    public void BuildScreen_WhenAncestorHidesDoneTasks_SkipsDoneDescendants()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var branch = map.AddAtCurrentNode("Branch");
        branch.HideDoneTasks = true;
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddAtCurrentNode("Subbranch");
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddAtCurrentNode("Open child");
        map.AddAtCurrentNode("Done child");
        Assert.True(map.SetTaskState("1", TaskState.Todo, out _));
        Assert.True(map.SetTaskState("2", TaskState.Done, out _));
        map.GoToRoot();
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        Assert.True(workflow.Execute(new ConsoleInput("cd 1")).IsSuccess);
        Assert.True(workflow.Execute(new ConsoleInput("cd 1")).IsSuccess);

        var screen = workflow.BuildScreen();

        Assert.Contains("Subbranch", screen);
        Assert.Contains("[ ] Open child", screen);
        Assert.DoesNotContain("Done child", screen);
    }

    [Fact]
    public void BuildScreen_ShowsGroupedHelpLines()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var screen = workflow.BuildScreen();
        var goToLine = GetLineContaining(screen, ColorLabel("Go to"));

        Assert.Contains(ColorLabel("Go to"), screen);
        Assert.Contains("text: ja", goToLine);
        Assert.Contains("numbers: 1", goToLine);
        Assert.DoesNotContain("...", goToLine);
        Assert.Contains(ColorLabel("To Do"), screen);
        Assert.Contains(ColorLabel("Navigate"), screen);
        Assert.Contains("hidedone [child]", screen);
        Assert.Contains("showdone [child]", screen);
    }

    [Fact]
    public void BuildScreen_ShowsCurrentAttachmentShortcuts()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var screen = workflow.BuildScreen();

        Assert.Contains(":Attachments>", screen);
        Assert.Contains("Capture.png", screen);
        Assert.Contains(ColorLabel("Search/Export"), screen);
        Assert.Contains("attachments [attachment]", screen);
    }

    [Fact]
    public void BuildScreen_WhenCommandsAreHidden_ShowsHintInsteadOfGroupedHelp()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var screen = workflow.BuildScreen(showCommands: false);

        Assert.DoesNotContain(ColorLabel("Go to"), screen);
        Assert.DoesNotContain(ColorLabel("Navigate"), screen);
        Assert.Contains(":i Commands hidden. Press \"~\" to show.", screen);
    }

    [Fact]
    public void BuildScreen_ShowsGoToShortcutListsWithoutEllipsis_WhenThereAreAtMostFiveChildren()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        for (var index = 0; index < 5; index++)
        {
            map.AddAtCurrentNode($"Child {index + 1}");
        }

        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var screen = workflow.BuildScreen();
        var goToLine = GetLineContaining(screen, ColorLabel("Go to"));

        Assert.Contains("text: ja, ka, fa, ad, da", goToLine);
        Assert.Contains("numbers: 1, 2, 3, 4, 5", goToLine);
        Assert.DoesNotContain("...", goToLine);
    }

    [Fact]
    public void BuildScreen_ShowsGoToShortcutListsWithEllipsis_WhenThereAreMoreThanFiveChildren()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        for (var index = 0; index < 6; index++)
        {
            map.AddAtCurrentNode($"Child {index + 1}");
        }

        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var screen = workflow.BuildScreen();
        var goToLine = GetLineContaining(screen, ColorLabel("Go to"));

        Assert.Contains("text: ja, ka, fa, ad, da, ...", goToLine);
        Assert.Contains("numbers: 1, 2, 3, 4, 5, ...", goToLine);
        Assert.DoesNotContain(AccessibleKeyNumbering.GetStringFor(6), goToLine);
        Assert.DoesNotContain("6", goToLine);
    }

    [Fact]
    public void BuildScreen_OmitsGoToLineWhenThereAreNoChildren()
    {
        using var workspace = new TestWorkspace();
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var screen = workflow.BuildScreen();

        Assert.DoesNotContain(ColorLabel("Go to"), screen);
        Assert.Contains(ColorLabel("To Do"), screen);
    }

    [Fact]
    public void BuildScreen_KeepsQueuedLinkNoticeAlongsideGroupedHelp()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("First");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        Assert.True(workflow.Execute(new ConsoleInput("linkfrom 1")).IsSuccess);

        var screen = workflow.BuildScreen();

        Assert.Contains(":Nodes to be linked>", screen);
        Assert.Contains(ColorLabel("Links"), screen);
    }

    private static Guid GetRequiredNodeIdentifier(Node node) =>
        node.UniqueIdentifier ?? throw new InvalidOperationException("Node identifier is required for attachment tests.");

    private static string ColorLabel(string label) =>
        $"[{ConfigurationConstants.CommandColor}]{label}[!]: ";

    private static string GetLineContaining(string content, string value) =>
        content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.Contains(value, StringComparison.InvariantCulture));
}
