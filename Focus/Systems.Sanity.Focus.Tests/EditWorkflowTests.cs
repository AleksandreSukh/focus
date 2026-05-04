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
    public void Execute_UppercaseTodoCommand_UpdatesCurrentNodeTaskState()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        Assert.True(workflow.Execute(new ConsoleInput("CD 1")).IsSuccess);

        var result = workflow.Execute(new ConsoleInput("TODO"));

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("Mark task as todo in workflow-map", result.SyncCommitMessage);
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
    public void Execute_StarAndUnstarNode_ReordersAndPersistsFlag()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("First");
        map.AddAtCurrentNode("Second");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var starResult = workflow.Execute(new ConsoleInput("star 2"));
        workflow.Save(starResult.SyncCommitMessage!);
        var starredMap = workspace.MapsStorage.OpenMap(filePath);

        var unstarWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        var unstarResult = unstarWorkflow.Execute(new ConsoleInput("unstar 1"));
        unstarWorkflow.Save(unstarResult.SyncCommitMessage!);
        var unstarredMap = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(starResult.IsSuccess);
        Assert.True(starResult.ShouldPersist);
        Assert.Equal("Star node in workflow-map", starResult.SyncCommitMessage);
        Assert.Equal("Second", starredMap.GetChildren()[1]);
        Assert.True(starredMap.GetNode("1")!.Starred);
        Assert.True(unstarResult.IsSuccess);
        Assert.True(unstarResult.ShouldPersist);
        Assert.Equal("Unstar node in workflow-map", unstarResult.SyncCommitMessage);
        Assert.Equal("Second", unstarredMap.GetChildren()[1]);
        Assert.False(unstarredMap.GetNode("1")!.Starred);
    }

    [Fact]
    public void BuildScreen_ShowsStarredMarkerWithoutChangingNodeName()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("star 1"));
        var screen = workflow.BuildScreen();
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(result.IsSuccess);
        Assert.Contains("* Child", screen);
        Assert.Equal("Child", reopened.RootNode.Children[0].Name);
    }

    [Fact]
    public void Execute_StarAtRoot_ReturnsValidationError()
    {
        using var workspace = new TestWorkspace();
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("star"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Can't change starred state for root node", result.ErrorString);
    }

    [Fact]
    public void Execute_AddBlock_CreatesMultilineTextBlockNode()
    {
        using var workspace = new TestWorkspace();
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("First line", "", "Second line", "", ""));

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("addblock"));
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var blockNode = Assert.Single(reopened.RootNode.Children);

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("Add block in workflow-map", result.SyncCommitMessage);
        Assert.Equal(NodeType.TextBlockItem, blockNode.NodeType);
        Assert.Equal("First line\n\nSecond line", blockNode.Name.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Execute_AddBlock_WithImmediateDoubleEnter_DoesNotCreateNode()
    {
        using var workspace = new TestWorkspace();
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("", ""));

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("addblock"));
        var reopened = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(result.IsSuccess);
        Assert.False(result.ShouldPersist);
        Assert.Empty(reopened.RootNode.Children);
    }

    [Fact]
    public void Execute_Edit_OnTextBlockNode_UpdatesMultilineContent()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddBlockAtCurrentNode("Old line");
        var filePath = workspace.SaveMap("workflow-map", map);
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("Updated line 1", "", "Updated line 2", "", ""));

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        Assert.True(workflow.Execute(new ConsoleInput("cd 1")).IsSuccess);

        var result = workflow.Execute(new ConsoleInput("edit"));
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var blockNode = Assert.Single(reopened.RootNode.Children);

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("Edit node in workflow-map", result.SyncCommitMessage);
        Assert.Equal(NodeType.TextBlockItem, blockNode.NodeType);
        Assert.Equal("Updated line 1\n\nUpdated line 2", blockNode.Name.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Execute_Edit_OnRootTextBlock_RenamesFileUsingFirstLinePreview()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap(new Node("Old title\nOld body", NodeType.TextBlockItem, 1));
        var filePath = workspace.SaveMap("old-title", map);
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("New title", "", "New body", "", ""));

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("edit"));
        workflow.Save(result.SyncCommitMessage!);

        var renamedFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "New title.json");
        var renamedMap = workspace.MapsStorage.OpenMap(renamedFilePath);

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.False(File.Exists(filePath));
        Assert.True(File.Exists(renamedFilePath));
        Assert.Equal("New title\n\nNew body", renamedMap.RootNode.Name.ReplaceLineEndings("\n"));
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
    public void Execute_AttachmentsCommand_OpensLegacyAttachmentAfterMigration()
    {
        var fileOpener = new RecordingFileOpener();
        using var workspace = new TestWorkspace(fileOpener: fileOpener);
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
        var legacyAttachmentPath = workspace.AppContext.MapsStorage.AttachmentStore.ResolveLegacyAttachmentPath(
            filePath,
            "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyAttachmentPath)!);
        File.WriteAllText(legacyAttachmentPath, "attachment");

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("attachments 1"));
        var migratedAttachmentPath = workspace.AppContext.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            filePath,
            GetRequiredNodeIdentifier(map.RootNode),
            "capture.png");

        Assert.True(result.IsSuccess);
        Assert.Equal("Opened attachment \"Capture.png\"", result.Message);
        Assert.Equal(migratedAttachmentPath, fileOpener.OpenedFilePath);
        Assert.True(File.Exists(migratedAttachmentPath));
        Assert.False(File.Exists(legacyAttachmentPath));
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
    public void Execute_ParentHideDoneRefreshesChildShowDoneOverride()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var branch = map.AddAtCurrentNode("Branch");
        var openChild = branch.Add("Open child");
        var doneChild = branch.Add("Done child");
        openChild.TaskState = TaskState.Todo;
        doneChild.TaskState = TaskState.Done;
        var filePath = workspace.SaveMap("workflow-map", map);

        var hideRootWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        var hideRootResult = hideRootWorkflow.Execute(new ConsoleInput("hidedone"));
        hideRootWorkflow.Save(hideRootResult.SyncCommitMessage!);

        var showBranchWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        var showBranchResult = showBranchWorkflow.Execute(new ConsoleInput("showdone 1"));
        showBranchWorkflow.Save(showBranchResult.SyncCommitMessage!);
        var shownMap = workspace.MapsStorage.OpenMap(filePath);

        var shownWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        Assert.True(shownWorkflow.Execute(new ConsoleInput("cd 1")).IsSuccess);
        var shownScreen = shownWorkflow.BuildScreen();

        var refreshWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        var refreshResult = refreshWorkflow.Execute(new ConsoleInput("hidedone"));
        refreshWorkflow.Save(refreshResult.SyncCommitMessage!);
        var refreshedMap = workspace.MapsStorage.OpenMap(filePath);

        var refreshedWorkflow = new EditWorkflow(filePath, workspace.AppContext);
        Assert.True(refreshedWorkflow.Execute(new ConsoleInput("cd 1")).IsSuccess);
        var refreshedScreen = refreshedWorkflow.BuildScreen();

        Assert.True(hideRootResult.IsSuccess);
        Assert.True(showBranchResult.IsSuccess);
        Assert.Contains("[x] Done child", shownScreen);
        Assert.False(shownMap.GetNode("1")!.HideDoneTasks);
        Assert.True(shownMap.GetNode("1")!.HideDoneTasksExplicit);
        Assert.True(refreshResult.IsSuccess);
        Assert.DoesNotContain("Done child", refreshedScreen);
        Assert.False(refreshedMap.GetNode("1")!.HideDoneTasks);
        Assert.Null(refreshedMap.GetNode("1")!.HideDoneTasksExplicit);
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
    public void Execute_AttachmentShortcut_NavigatesToVisibleChildBeforeOpeningAttachment()
    {
        var fileOpener = new RecordingFileOpener();
        using var workspace = new TestWorkspace(fileOpener: fileOpener);
        var map = new MindMap("Root");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        map.AddAtCurrentNode("Visible child");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput(AccessibleKeyNumbering.GetStringFor(1)));

        Assert.True(result.IsSuccess);
        Assert.Null(fileOpener.OpenedFilePath);
        Assert.Contains("Visible child", workflow.BuildScreen());
    }

    [Fact]
    public void Execute_AttachmentShortcut_OpensAttachmentWhenMatchingChildShortcutIsHiddenDone()
    {
        var fileOpener = new RecordingFileOpener();
        using var workspace = new TestWorkspace(fileOpener: fileOpener);
        var map = new MindMap("Root");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        map.AddAtCurrentNode("Done child");
        Assert.True(map.SetTaskState("1", TaskState.Done, out _));
        Assert.True(map.SetHideDoneTasks(true, out _));
        var filePath = workspace.SaveMap("workflow-map", map);
        var attachmentPath = workspace.AppContext.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            filePath,
            GetRequiredNodeIdentifier(map.RootNode),
            "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(attachmentPath)!);
        File.WriteAllText(attachmentPath, "attachment");

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput(AccessibleKeyNumbering.GetStringFor(1)));

        Assert.True(result.IsSuccess);
        Assert.Equal(attachmentPath, fileOpener.OpenedFilePath);
        Assert.DoesNotContain("Done child", workflow.BuildScreen());
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
        Assert.Contains("addblock", screen);
        Assert.Contains("hidedone [child]", screen);
        Assert.Contains("showdone [child]", screen);
    }

    [Fact]
    public void GetSuggestions_WithoutChildren_IncludesCommandsAndTaskFilters()
    {
        using var workspace = new TestWorkspace();
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var suggestions = workflow.GetSuggestions().ToArray();

        Assert.Contains("addblock", suggestions);
        Assert.Contains("todo", suggestions);
        Assert.Contains("td", suggestions);
        Assert.Contains("tasks done", suggestions);
        Assert.Contains("ts all", suggestions);
        Assert.DoesNotContain("cd 1", suggestions);
    }

    [Fact]
    public void GetSuggestions_WithChildren_IncludesChildParameterSuggestions()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var suggestions = workflow.GetSuggestions().ToArray();

        Assert.Contains("cd 1", suggestions);
        Assert.Contains("edit 1", suggestions);
        Assert.Contains("todo 1", suggestions);
        Assert.Contains("td 1", suggestions);
        Assert.Contains("star 1", suggestions);
        Assert.Contains("unstar Child", suggestions);
        Assert.Contains("edit Child", suggestions);
        Assert.Contains("search Child", suggestions);
        Assert.DoesNotContain("clearideas 1", suggestions);
    }

    [Fact]
    public void BuildScreen_RendersTextBlocksAsQuotedBlocks()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddBlockAtCurrentNode("Block title\n\nBlock body");
        var filePath = workspace.SaveMap("workflow-map", map);

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var screen = workflow.BuildScreen();

        Assert.Contains("[block] Block title", screen);
        Assert.Contains("> Block title", screen);
        Assert.Contains("> Block body", screen);
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
        Assert.Contains($":i {CommandHelpText.HiddenHelpMessage}", screen);
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

    [Fact]
    public void Execute_Add_UsesWorkflowInteractionAndPersistsReportedChanges()
    {
        var interactions = new RecordingWorkflowInteractions
        {
            AddNotesResult = true,
            AddNotesAction = (map, _) => map.AddAtCurrentNode("Added through interaction")
        };
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("add"));
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("Add note in workflow-map", result.SyncCommitMessage);
        Assert.Single(interactions.AddNotesInitialInputs);
        Assert.Null(interactions.AddNotesInitialInputs.Single());
        Assert.Contains(reopened.GetChildren().Values, value => value == "Added through interaction");
    }

    [Fact]
    public void Execute_DeleteChild_UsesWorkflowInteractionConfirmationCancellation()
    {
        var interactions = new RecordingWorkflowInteractions
        {
            DefaultConfirmationResult = false
        };
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("del 1"));

        Assert.False(result.IsSuccess);
        Assert.False(result.ShouldPersist);
        Assert.Equal("Cancelled!", result.ErrorString);
        Assert.Equal(["Are you sure to delete \"1\". \"Child\""], interactions.ConfirmationMessages);
        Assert.Contains("Child", workflow.BuildScreen());
    }

    [Fact]
    public void Execute_Search_UsesWorkflowInteractionResultSelection()
    {
        var interactions = new RecordingWorkflowInteractions
        {
            SearchResultSelector = results => results.Single()
        };
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Unique Search Result");
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var searchResult = workflow.Execute(new ConsoleInput("search Unique"));
        var todoResult = workflow.Execute(new ConsoleInput("todo"));
        workflow.Save(todoResult.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(searchResult.IsSuccess);
        Assert.True(todoResult.IsSuccess);
        Assert.Equal(TaskState.Todo, reopened.GetNode("1")!.TaskState);
        Assert.Equal(["Search results for \"Unique\""], interactions.SearchSelectionTitles);
        Assert.False(interactions.SearchSelectionDisplayOptions.Single().IncludeMapName);
    }

    [Fact]
    public void Execute_UnknownInputFallback_ConfirmsAndAddsCurrentInputThroughWorkflowInteraction()
    {
        var interactions = new RecordingWorkflowInteractions
        {
            AddNotesResult = true,
            AddNotesAction = (map, initialInput) => map.AddAtCurrentNode(initialInput ?? string.Empty)
        };
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Existing child");
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("new child from fallback"));

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("Add note in workflow-map", result.SyncCommitMessage);
        Assert.Equal(["new child from fallback"], interactions.AddNotesInitialInputs);
        Assert.Contains("Did you mean to add new record?", interactions.ConfirmationMessages.Single());
        Assert.Contains("new child from fallback", workflow.BuildScreen());
    }

    [Fact]
    public void Execute_Tasks_UsesWorkflowInteractionResultSelection()
    {
        var interactions = new RecordingWorkflowInteractions
        {
            SearchResultSelector = results => results.Single(result => result.NodeName == "Done task")
        };
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Todo task");
        map.AddAtCurrentNode("Done task");
        Assert.True(map.SetTaskState("1", TaskState.Todo, out _));
        Assert.True(map.SetTaskState("2", TaskState.Done, out _));
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("tasks all"));

        Assert.True(result.IsSuccess);
        Assert.Contains("[x] Done task", workflow.BuildScreen());
        Assert.Equal(["tasks in current map"], interactions.SearchSelectionTitles);
        Assert.False(interactions.SearchSelectionDisplayOptions.Single().IncludeMapName);
    }

    [Fact]
    public void Execute_OpenLink_UsesWorkflowInteractionResultSelection()
    {
        var interactions = new RecordingWorkflowInteractions
        {
            SearchResultSelector = results => results.Single(result => result.NodeName == "Linked child")
        };
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Linked child");
        Assert.True(map.LinkToNode("1", map.RootNode, LinkRelationType.Relates, "metadata"));
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("openlink"));

        Assert.True(result.IsSuccess);
        Assert.Contains("Linked child", workflow.BuildScreen());
        Assert.Contains("Linked nodes", interactions.SearchSelectionTitles.Single());
        Assert.True(interactions.SearchSelectionDisplayOptions.Single().IncludeMapName);
    }

    [Fact]
    public void Execute_SliceOutChild_OpensCreateMapWithDetachedMap()
    {
        var navigator = new RecordingPageNavigator();
        var interactions = new RecordingWorkflowInteractions();
        interactions.EnqueueOptionSelection(2);
        using var workspace = new TestWorkspace(navigator, workflowInteractions: interactions);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Detached child");
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("slice 1"));

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("Detach node from workflow-map", result.SyncCommitMessage);
        Assert.Equal("Detached child", navigator.OpenedCreateMapFileName);
        Assert.NotNull(navigator.OpenedCreateMapMindMap);
        Assert.Equal("Detached child", navigator.OpenedCreateMapMindMap!.RootNode.Name);
    }

    private static Guid GetRequiredNodeIdentifier(Node node) =>
        node.UniqueIdentifier ?? throw new InvalidOperationException("Node identifier is required for attachment tests.");

    private static string ColorLabel(string label) =>
        $"[{ConfigurationConstants.CommandColor}]{label}[!]: ";

    private static string GetLineContaining(string content, string value) =>
        content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.Contains(value, StringComparison.InvariantCulture));
}
