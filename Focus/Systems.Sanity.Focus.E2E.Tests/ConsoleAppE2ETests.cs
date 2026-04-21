using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.E2E.Tests;

public class ConsoleAppE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Launch_WithConfigOverride_ShowsHomePage_AndExitCleanly()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("Error occured while starting application.", app.GetTranscript(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(workspace.HomeDirectory, "focus-config.json")));
    }

    [Fact]
    public async Task Launch_WithoutConfig_CreatesConfig_ThroughTestHostPrompt()
    {
        using var workspace = new FocusE2EWorkspace();
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Press \"Enter\" to save data into Documents folder"),
            FocusScenario.SendLine(workspace.DataFolder),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(workspace.ConfigFilePath));

        var config = JsonSerializer.Deserialize<UserConfig>(File.ReadAllText(workspace.ConfigFilePath), JsonOptions)
                     ?? throw new InvalidOperationException("Config was not written.");
        Assert.Equal(workspace.DataFolder, config.DataFolder);
    }

    [Fact]
    public async Task InvalidHomeInput_ReadKeyReplay_AllowsExit()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("bogus"),
            FocusScenario.WaitForOutput("*** Wrong Input ***"),
            FocusScenario.SendKey(new ConsoleKeyInfo('e', ConsoleKey.E, shift: false, alt: false, control: false)),
            FocusScenario.SendLine("xit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("*** Wrong Input ***", app.GetTranscript(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMap_AddNode_SetTaskState_AndPersistJson()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("new alpha"),
            FocusScenario.WaitForOutput("Navigate"),
            FocusScenario.SendLine("first task"),
            FocusScenario.SendLine(string.Empty),
            FocusScenario.SendLine("todo 1"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertTaskState("alpha.json", "1", TaskState.Todo));
    }

    [Fact]
    public async Task Home_OpensMap_ByNumberShortcutAndName()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        SaveMap(workspace, "alpha", new MindMap("Alpha root"));
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("ja"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Home_RenamesAndDeletesMaps()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        SaveMap(workspace, "alpha", new MindMap("Alpha"));
        SaveMap(workspace, "beta", new MindMap("Beta"));
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("ren alpha"),
            FocusScenario.WaitForOutput("Enter new name here:"),
            FocusScenario.SendLine("renamed-home-map"),
            FocusScenario.WaitForOutput("renamed-home-map"),
            FocusScenario.SendLine("del beta"),
            FocusScenario.WaitForOutput("Type \"yes\" to confirm or \"Enter\" to cancel"),
            FocusScenario.SendLine("yes"),
            FocusScenario.WaitForOutput("renamed-home-map"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(workspace.MapsDirectory, "alpha.json")));
        Assert.False(File.Exists(Path.Combine(workspace.MapsDirectory, "beta.json")));
        Assert.True(File.Exists(Path.Combine(workspace.MapsDirectory, "renamed-home-map.json")));

        var renamedMap = MapFile.OpenFile(Path.Combine(workspace.MapsDirectory, "renamed-home-map.json"));
        Assert.Equal("renamed-home-map", renamedMap.RootNode.Name);
    }

    [Fact]
    public async Task Home_SearchAndTasks_OpenSelectedResults()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var searchMap = new MindMap("Search Root");
        searchMap.AddAtCurrentNode("Unique Search Result");
        SaveMap(workspace, "search-map", searchMap);

        var taskMap = new MindMap("Task Root");
        taskMap.AddAtCurrentNode("Todo Result");
        Assert.True(taskMap.SetTaskState("1", TaskState.Todo, out _));
        SaveMap(workspace, "task-map", taskMap);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("search Unique"),
            FocusScenario.WaitForOutput("Search results for \"Unique\""),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("Navigate"),
            FocusScenario.SendLine("up"),
            FocusScenario.WaitForOutput("Search Root"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("tasks todo"),
            FocusScenario.WaitForOutput("Todo Result"),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("Navigate"),
            FocusScenario.SendLine("up"),
            FocusScenario.WaitForOutput("Task Root"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task EditMap_AddEditDeleteAndClearIdeas_PersistJson()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        SaveMap(workspace, "alpha", new MindMap("Alpha root"));
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("add"),
            FocusScenario.SendLine("Editable child"),
            FocusScenario.SendLine("Delete me"),
            FocusScenario.SendLine(string.Empty),
            FocusScenario.SendLine("cd 1"),
            FocusScenario.SendLine("idea"),
            FocusScenario.SendLine("First idea"),
            FocusScenario.SendLine(string.Empty),
            FocusScenario.SendLine("edit"),
            FocusScenario.WaitForOutput("Enter new text here:"),
            FocusScenario.SendLine("Edited child"),
            FocusScenario.SendLine("up"),
            FocusScenario.SendLine("clearideas 1"),
            FocusScenario.WaitForOutput("Clear idea tags for:"),
            FocusScenario.SendLine("yes"),
            FocusScenario.SendLine("del 2"),
            FocusScenario.WaitForOutput("Are you sure to delete"),
            FocusScenario.SendLine("yes"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", map =>
            {
                Assert.Equal("Alpha root", map.RootNode.Name);
                Assert.Collection(
                    map.RootNode.Children,
                    child =>
                    {
                        Assert.Equal("Edited child", child.Name);
                        Assert.Empty(child.Children);
                    });
            }));
    }

    [Fact]
    public async Task EditMap_ImplicitAddOnEmptyMap_AndClearIdeasOnCurrentNode()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        SaveMap(workspace, "alpha", new MindMap("Alpha root"));
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("Implicit child"),
            FocusScenario.SendLine(string.Empty),
            FocusScenario.SendLine("1"),
            FocusScenario.SendLine("idea Seed idea"),
            FocusScenario.SendLine(string.Empty),
            FocusScenario.SendLine("clearideas"),
            FocusScenario.WaitForOutput("Clear idea tags for current node?"),
            FocusScenario.SendLine("yes"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", map =>
            {
                Assert.Collection(
                    map.RootNode.Children,
                    child =>
                    {
                        Assert.Equal("Implicit child", child.Name);
                        Assert.Empty(child.Children);
                    });
            }));
    }

    [Fact]
    public async Task EditMap_RenamingRootNode_RenamesBackingFile()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        SaveMap(workspace, "alpha", new MindMap("Alpha root"));
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("edit"),
            FocusScenario.WaitForOutput("Enter new text here:"),
            FocusScenario.SendLine("Renamed Root"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(workspace.MapsDirectory, "alpha.json")));
        Assert.True(File.Exists(Path.Combine(workspace.MapsDirectory, "Renamed Root.json")));

        var renamedMap = MapFile.OpenFile(Path.Combine(workspace.MapsDirectory, "Renamed Root.json"));
        Assert.Equal("Renamed Root", renamedMap.RootNode.Name);
    }

    [Fact]
    public async Task Navigation_CdShortcutUpAndLs_MoveBetweenCurrentNodes()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var map = new MindMap("Alpha root");
        map.AddAtCurrentNode("Branch node");
        map.AddAtCurrentNode("Sibling leaf");
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddAtCurrentNode("Nested leaf");
        map.GoToRoot();
        SaveMap(workspace, "alpha", map);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("| * Alpha root"),
            FocusScenario.SendLine("cd 1"),
            FocusScenario.WaitForOutput("| * Branch node"),
            FocusScenario.SendLine("ja"),
            FocusScenario.WaitForOutput("| * Nested leaf"),
            FocusScenario.SendLine("up"),
            FocusScenario.WaitForOutputOccurrences("| * Branch node", 2),
            FocusScenario.SendLine("ls"),
            FocusScenario.WaitForOutputOccurrences("| * Alpha root", 2),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Navigation_MinAndMax_PersistCollapsedViewState()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var map = new MindMap("Alpha root");
        map.AddAtCurrentNode("Collapse target");
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddAtCurrentNode("Leaf one");
        map.AddAtCurrentNode("Leaf two");
        map.AddAtCurrentNode("Leaf three");
        map.GoToRoot();
        SaveMap(workspace, "alpha", map);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("| * Alpha root"),
            FocusScenario.SendLine("min 1"),
            FocusScenario.WaitForOutput("Collapse target ///"),
            FocusScenario.AssertMap("alpha.json", savedMap => Assert.True(savedMap.RootNode.Children[0].IsCollapsed())),
            FocusScenario.SendLine("max 1"),
            FocusScenario.WaitForOutputOccurrences("| * Alpha root", 3),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", savedMap => Assert.False(savedMap.RootNode.Children[0].IsCollapsed())));
    }

    [Fact]
    public async Task TaskWorkflows_HideDoneTasks_HidesAndRestoresDoneDescendants()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var map = new MindMap("Alpha root");
        var branch = map.AddAtCurrentNode("Branch");
        var openChild = branch.Add("Open child");
        var doneChild = branch.Add("Done child");
        openChild.TaskState = TaskState.Todo;
        doneChild.TaskState = TaskState.Done;
        SaveMap(workspace, "alpha", map);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("| * Alpha root"),
            FocusScenario.WaitForOutput("Done child"));

        var transcriptAfterOpeningMap = app.GetTranscript().Length;

        await FocusScenario.RunAsync(
            context,
            FocusScenario.SendLine("hidedone 1"),
            FocusScenario.WaitForOutputOccurrences("| * Alpha root", 2),
            FocusScenario.AssertMap("alpha.json", savedMap => Assert.True(savedMap.RootNode.Children[0].HideDoneTasks)),
            FocusScenario.SendLine("cd 1"),
            FocusScenario.WaitForOutputOccurrences("Open child", 2));

        Assert.DoesNotContain("[x] Done child", app.GetTranscript()[transcriptAfterOpeningMap..], StringComparison.Ordinal);

        var transcriptAfterHideDone = app.GetTranscript().Length;

        await FocusScenario.RunAsync(
            context,
            FocusScenario.SendLine("showdone"),
            FocusScenario.WaitForOutputOccurrences("Done child", 2),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        Assert.Contains("[x] Done child", app.GetTranscript()[transcriptAfterHideDone..], StringComparison.Ordinal);

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", savedMap => Assert.False(savedMap.RootNode.Children[0].HideDoneTasks)));
    }

    [Fact]
    public async Task ViewState_Tilde_TogglesCommandHelp()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        SaveMap(workspace, "alpha", new MindMap("Alpha root"));
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Navigate"),
            FocusScenario.SendKey(new ConsoleKeyInfo('~', ConsoleKey.Oem3, shift: true, alt: false, control: false)),
            FocusScenario.WaitForOutput("Commands hidden. Press \"~\" to show."),
            FocusScenario.SendKey(new ConsoleKeyInfo('~', ConsoleKey.Oem3, shift: true, alt: false, control: false)),
            FocusScenario.WaitForOutputOccurrences("Navigate", 2),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Navigation_Ls_ReloadsEditorAfterCollaboratorChangesMap()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));

        var initialMap = new MindMap("Alpha root");
        initialMap.AddAtCurrentNode("Local child");
        gitSandbox.WriteCollaboratorMap("alpha", initialMap);
        gitSandbox.CommitAndPushCollaborator("Add alpha");

        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);
        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("ls"),
            FocusScenario.WaitForOutput("alpha"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("| * Alpha root"),
            FocusScenario.SendLine("cd 1"),
            FocusScenario.WaitForOutput("| * Local child"));

        var updatedMap = new MindMap("Remote root");
        updatedMap.AddAtCurrentNode("Remote child");
        gitSandbox.WriteCollaboratorMap("alpha", updatedMap);
        gitSandbox.CommitAndPushCollaborator("Update alpha");

        await FocusScenario.RunAsync(
            context,
            FocusScenario.SendLine("ls"),
            FocusScenario.WaitForOutput("| * Remote root"),
            FocusScenario.WaitForOutput("Remote child"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", savedMap =>
            {
                Assert.Equal("Remote root", savedMap.RootNode.Name);
                Assert.Collection(
                    savedMap.RootNode.Children,
                    child => Assert.Equal("Remote child", child.Name));
            }));
    }

    [Fact]
    public async Task TaskWorkflows_TodoDoingDoneToggleAndNotask_PersistJson()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var map = new MindMap("Alpha root");
        map.AddAtCurrentNode("Todo via parameter");
        map.AddAtCurrentNode("Doing via parameter");
        map.AddAtCurrentNode("Done via parameter");
        map.AddAtCurrentNode("Current task");
        SaveMap(workspace, "alpha", map);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("todo 1"),
            FocusScenario.SendLine("doing 2"),
            FocusScenario.SendLine("done 3"),
            FocusScenario.SendLine("cd 4"),
            FocusScenario.WaitForOutput("| * Current task"),
            FocusScenario.SendLine("todo"),
            FocusScenario.SendLine("toggle"),
            FocusScenario.WaitForOutput("| * [x] Current task"),
            FocusScenario.SendLine("notask"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertTaskState("alpha.json", "1", TaskState.Todo),
            FocusScenario.AssertTaskState("alpha.json", "2", TaskState.Doing),
            FocusScenario.AssertTaskState("alpha.json", "3", TaskState.Done),
            FocusScenario.AssertTaskState("alpha.json", "4", TaskState.None));
    }

    [Fact]
    public async Task TaskWorkflows_MapLocalTasks_OpenSelectedTaskNode()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var map = new MindMap("Alpha root");
        map.AddAtCurrentNode("Project");
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddAtCurrentNode("Todo nested");
        map.AddAtCurrentNode("Doing nested");
        map.AddAtCurrentNode("Done nested");
        Assert.True(map.SetTaskState("1", TaskState.Todo, out _));
        Assert.True(map.SetTaskState("2", TaskState.Doing, out _));
        Assert.True(map.SetTaskState("3", TaskState.Done, out _));
        map.GoToRoot();
        SaveMap(workspace, "alpha", map);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("cd 1"),
            FocusScenario.WaitForOutput("| * Project"),
            FocusScenario.SendLine("tasks"),
            FocusScenario.WaitForOutput("[~] Alpha root > Project > Doing nested"),
            FocusScenario.WaitForOutput("[ ] Alpha root > Project > Todo nested"),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("| * [~] Doing nested"),
            FocusScenario.SendLine("up"),
            FocusScenario.WaitForOutputOccurrences("| * Project", 2),
            FocusScenario.SendLine("tasks done"),
            FocusScenario.WaitForOutput("[x] Alpha root > Project > Done nested"),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("| * [x] Done nested"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task TaskWorkflows_HomeTasks_AcrossAllMaps_OpenSelectedTaskNode()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var alphaMap = new MindMap("Alpha root");
        alphaMap.AddAtCurrentNode("Todo task");
        Assert.True(alphaMap.SetTaskState("1", TaskState.Todo, out _));
        SaveMap(workspace, "alpha", alphaMap);

        var betaMap = new MindMap("Beta root");
        betaMap.AddAtCurrentNode("Doing task");
        Assert.True(betaMap.SetTaskState("1", TaskState.Doing, out _));
        SaveMap(workspace, "beta", betaMap);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("tasks"),
            FocusScenario.WaitForOutput("beta: [~] Beta root > Doing task"),
            FocusScenario.WaitForOutput("alpha: [ ] Alpha root > Todo task"),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("| * [~] Doing task"),
            FocusScenario.SendLine("up"),
            FocusScenario.WaitForOutput("| * Beta root"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Search_InMap_OpensSelectedResult()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var map = new MindMap("Alpha root");
        map.AddAtCurrentNode("Branch");
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddAtCurrentNode("Unique nested match");
        map.GoToRoot();
        SaveMap(workspace, "alpha", map);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("search Unique nested"),
            FocusScenario.WaitForOutput("Search results for \"Unique nested\""),
            FocusScenario.WaitForOutput("Alpha root > Branch > Unique nested match"),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("| * Unique nested match"),
            FocusScenario.SendLine("up"),
            FocusScenario.WaitForOutput("| * Branch"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task GraphNavigation_LinkFromLinkToOpenLinkAndBacklinks_NavigateBetweenNodes()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var map = new MindMap("Alpha root");
        map.AddAtCurrentNode("Source node");
        map.AddAtCurrentNode("Target node");
        var targetNodeId = map.GetNode("2")?.UniqueIdentifier
                           ?? throw new InvalidOperationException("Target node id was not created.");
        SaveMap(workspace, "alpha", map);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("linkfrom 1"),
            FocusScenario.WaitForOutput(":Nodes to be linked> Source node"),
            FocusScenario.SendLine("linkto 2"),
            FocusScenario.WaitForOutput("*** Select Option ***"),
            FocusScenario.WaitForOutput("prerequisite"),
            FocusScenario.SendLine("2"),
            FocusScenario.WaitForOutput("Type \"yes\" to confirm or \"Enter\" to cancel"),
            FocusScenario.SendLine("yes"),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("| * Source node"),
            FocusScenario.SendLine("openlink"),
            FocusScenario.WaitForOutput("Target node [prerequisite]"),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("| * Target node"),
            FocusScenario.SendLine("backlinks"),
            FocusScenario.WaitForOutput("Source node [backlink: prerequisite]"),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutputOccurrences("| * Source node", 2),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", savedMap =>
            {
                var sourceNode = savedMap.GetNode("1") ?? throw new InvalidOperationException("Source node missing.");
                Assert.True(sourceNode.Links.TryGetValue(targetNodeId, out var link));
                Assert.Equal(LinkRelationType.Prerequisite, link!.relationType);
            }));
    }

    [Fact]
    public async Task MapComposition_SliceOut_CreatesNewMapAndRemovesNodeFromSource()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var map = new MindMap("Alpha root");
        map.AddAtCurrentNode("Detached branch");
        map.AddAtCurrentNode("Stay put");
        Assert.True(map.ChangeCurrentNode("1"));
        map.AddAtCurrentNode("Inside detached");
        map.GoToRoot();
        SaveMap(workspace, "alpha", map);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("cd 1"),
            FocusScenario.WaitForOutput("| * Detached branch"),
            FocusScenario.SendLine("slice"),
            FocusScenario.WaitForOutput("*** Select Option ***"),
            FocusScenario.WaitForOutput("\"2\" or \"out\""),
            FocusScenario.SendLine("2"),
            FocusScenario.WaitForOutput("Detach current node? Detached branch"),
            FocusScenario.SendLine("yes"),
            FocusScenario.WaitForOutput("| * Detached branch"),
            FocusScenario.WaitForOutput("Inside detached"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutputOccurrences("| * Alpha root", 2),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(workspace.MapsDirectory, "alpha.json")));
        Assert.True(File.Exists(Path.Combine(workspace.MapsDirectory, "Detached branch.json")));

        var sourceMap = MapFile.OpenFile(Path.Combine(workspace.MapsDirectory, "alpha.json"));
        Assert.Collection(
            sourceMap.RootNode.Children,
            child => Assert.Equal("Stay put", child.Name));

        var detachedMap = MapFile.OpenFile(Path.Combine(workspace.MapsDirectory, "Detached branch.json"));
        Assert.Equal("Detached branch", detachedMap.RootNode.Name);
        Assert.Collection(
            detachedMap.RootNode.Children,
            child => Assert.Equal("Inside detached", child.Name));
    }

    [Fact]
    public async Task MapComposition_SliceIn_AttachesExistingMapAndDeletesSourceFile()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        SaveMap(workspace, "target", new MindMap("Target root"));

        var sourceMap = new MindMap("Source root");
        sourceMap.AddAtCurrentNode("Source child");
        SaveMap(workspace, "source", sourceMap);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("target"),
            FocusScenario.WaitForOutput("Target root"),
            FocusScenario.SendLine("slice"),
            FocusScenario.WaitForOutput("*** Select Option ***"),
            FocusScenario.WaitForOutput("\"1\" or \"in\""),
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("*** Attach Map ***"),
            FocusScenario.WaitForOutput("source"),
            FocusScenario.SendLine("source"),
            FocusScenario.WaitForOutput("Source root"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(workspace.MapsDirectory, "source.json")));
        Assert.True(File.Exists(Path.Combine(workspace.MapsDirectory, "target.json")));

        var targetMap = MapFile.OpenFile(Path.Combine(workspace.MapsDirectory, "target.json"));
        Assert.Collection(
            targetMap.RootNode.Children,
            child =>
            {
                Assert.Equal("Source root", child.Name);
                Assert.Collection(
                    child.Children,
                    nested => Assert.Equal("Source child", nested.Name));
            });
    }

    [Fact]
    public async Task Attachments_CaptureClipboardText_ListsAttachment_AndShowsMetadata()
    {
        const string clipboardText = "Unicode note \u041f\u0440\u0438\u0432\u0435\u0442 \u043c\u0438\u0440\nwith multiple lines and more details for preview handling";
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        SaveMap(workspace, "alpha", new MindMap("Root"));

        await using var app = new FocusAppProcessHarness(
            workspace,
            new FocusAppLaunchOptions
            {
                ClipboardText = clipboardText
            });
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("| * Root"),
            FocusScenario.SendLine("capture"),
            FocusScenario.WaitForOutput("Captured clipboard text into \"Root\""),
            FocusScenario.WaitForOutput(":Attachments>"),
            FocusScenario.WaitForOutput("Clipboard text "),
            FocusScenario.SendLine("meta"),
            FocusScenario.WaitForOutput("*** Metadata for Root ***"),
            FocusScenario.WaitForOutput("Attachments: 1"),
            FocusScenario.SendKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift: false, alt: false, control: false)),
            FocusScenario.WaitForOutputOccurrences(":Attachments>", 2),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", savedMap =>
            {
                var attachment = Assert.Single(savedMap.RootNode.Metadata!.Attachments);
                Assert.Equal("text/plain; charset=utf-8", attachment.MediaType);

                var attachmentPath = Path.Combine(
                    workspace.MapsDirectory,
                    "alpha_attachments",
                    attachment.RelativePath);
                Assert.True(File.Exists(attachmentPath));
                Assert.Equal(clipboardText, File.ReadAllText(attachmentPath, Encoding.UTF8));
            }));
    }

    [Fact]
    public async Task Attachments_CaptureClipboardImage_AndOpenBySelector()
    {
        var clipboardImageBytes = Encoding.UTF8.GetBytes("fake-png");
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        SaveMap(workspace, "alpha", new MindMap("Root"));

        await using var app = new FocusAppProcessHarness(
            workspace,
            new FocusAppLaunchOptions
            {
                ClipboardImageBytes = clipboardImageBytes
            });
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("| * Root"),
            FocusScenario.SendLine("capture"),
            FocusScenario.WaitForOutput("Captured clipboard image into \"Root\""),
            FocusScenario.WaitForOutput(":Attachments>"),
            FocusScenario.WaitForOutput("Screenshot "),
            FocusScenario.SendLine("attachments 1"),
            FocusScenario.WaitForOutput("Opened attachment \"Screenshot "),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", savedMap =>
            {
                var attachment = Assert.Single(savedMap.RootNode.Metadata!.Attachments);
                Assert.Equal("image/png", attachment.MediaType);

                var attachmentPath = Path.Combine(
                    workspace.MapsDirectory,
                    "alpha_attachments",
                    attachment.RelativePath);
                Assert.True(File.Exists(attachmentPath));
                Assert.Equal(clipboardImageBytes, File.ReadAllBytes(attachmentPath));

                Assert.Collection(
                    workspace.ReadOpenedFiles(),
                    openedPath => Assert.Equal(attachmentPath, openedPath));
            }));
    }

    [Fact]
    public async Task Attachments_MissingFile_ShowsError_WithoutOpeningAnything()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();

        var map = new MindMap("Root");
        map.RootNode.Metadata = NodeMetadata.Create(NodeMetadataSources.Manual, device: null, timestampUtc: DateTimeOffset.UtcNow);
        map.RootNode.Metadata.Attachments.Add(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "missing.png",
            MediaType = "image/png",
            DisplayName = "Missing.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        SaveMap(workspace, "alpha", map);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput(":Attachments>"),
            FocusScenario.SendLine("attachments 1"),
            FocusScenario.WaitForOutput("Attachment \"Missing.png\" is missing"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(workspace.ReadOpenedFiles());
    }

    [Fact]
    public async Task ExportMarkdown_WithAttachmentsCustomNameAndFullScope_CreatesFileAndPushesCommit()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        var map = new MindMap("Alpha root");
        var child = map.AddAtCurrentNode("Collapsed child");
        child.Collapsed = true;
        child.Add("Grandchild");
        AddTextAttachment(
            map.RootNode,
            relativePath: "note.txt",
            displayName: "Clipboard text.txt");
        WriteTextAttachment(
            gitSandbox.WorkingMapsDirectory,
            "alpha.json",
            relativePath: "note.txt",
            content: "Attached note");
        gitSandbox.WriteWorkingMap("alpha", map);
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("export"),
            FocusScenario.WaitForOutput("*** Export ***"),
            FocusScenario.SendLine("attachments"),
            FocusScenario.WaitForOutput("Attachments will be included"),
            FocusScenario.SendLine("name quarterly plan"),
            FocusScenario.WaitForOutput("File name set to \"quarterly plan\""),
            FocusScenario.SendLine("save"),
            FocusScenario.WaitForOutput("Exported markdown to \"quarterly plan.md\""),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertExportExists("quarterly plan.md"),
            FocusScenario.AssertGitCommit("Export markdown from alpha", GitCommitTarget.Remote));

        var markdown = File.ReadAllText(context.ResolveExportPath("quarterly plan.md"));
        Assert.Contains("# Alpha root", markdown);
        Assert.Contains("> Attached note", markdown);
        Assert.Contains("Collapsed child", markdown);
        Assert.Contains("Grandchild", markdown);
    }

    [Fact]
    public async Task ExportHtml_WithBlackBackgroundCollapsedScopeAndRenameCollision_UsesSuggestedName()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        var map = new MindMap("Alpha root");
        var child = map.AddAtCurrentNode("Collapsed child");
        child.Collapsed = true;
        child.Add("Grandchild");
        AddTextAttachment(
            map.RootNode,
            relativePath: "note.txt",
            displayName: "Clipboard text.txt");
        WriteTextAttachment(
            gitSandbox.WorkingMapsDirectory,
            "alpha.json",
            relativePath: "note.txt",
            content: "Attached note");
        gitSandbox.WriteWorkingMap("alpha", map);
        Directory.CreateDirectory(gitSandbox.WorkingMapsDirectory);
        File.WriteAllText(Path.Combine(gitSandbox.WorkingMapsDirectory, "report.html"), "existing export");
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("export"),
            FocusScenario.WaitForOutput("*** Export ***"),
            FocusScenario.SendLine("html"),
            FocusScenario.WaitForOutput("Format set to HTML"),
            FocusScenario.SendLine("blackbg"),
            FocusScenario.WaitForOutput("Background set to black"),
            FocusScenario.SendLine("collapsed"),
            FocusScenario.WaitForOutput("Collapsed descendants will be skipped"),
            FocusScenario.SendLine("attachments"),
            FocusScenario.WaitForOutput("Attachments will be included"),
            FocusScenario.SendLine("noattachments"),
            FocusScenario.WaitForOutput("Attachments will be excluded"),
            FocusScenario.SendLine("name report"),
            FocusScenario.WaitForOutput("File name set to \"report\""),
            FocusScenario.SendLine("save"),
            FocusScenario.WaitForOutput("File: report already exists. Use suggested name report_(2)"),
            FocusScenario.SendLine(string.Empty),
            FocusScenario.WaitForOutput("Exported HTML to \"report_(2).html\" (collapsed descendants skipped)"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertExportExists("report_(2).html"),
            FocusScenario.AssertGitCommit("Export HTML from alpha", GitCommitTarget.Remote));

        var html = File.ReadAllText(context.ResolveExportPath("report_(2).html"));
        Assert.Contains(":root { color-scheme: dark; }", html);
        Assert.Contains("background: #000000;", html);
        Assert.Contains("Collapsed child", html);
        Assert.DoesNotContain("Grandchild", html);
        Assert.DoesNotContain("Attached note", html);
        Assert.DoesNotContain("alpha_attachments/note.txt", html);
    }

    [Fact]
    public async Task Export_Cancel_ReturnsToEditorWithoutCreatingFileOrSyncing()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        gitSandbox.WriteWorkingMap("alpha", new MindMap("alpha"));
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);

        await using var app = new FocusAppProcessHarness(workspace);
        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Navigate"),
            FocusScenario.SendLine("export"),
            FocusScenario.WaitForOutput("*** Export ***"),
            FocusScenario.SendLine("html"),
            FocusScenario.WaitForOutput("Format set to HTML"),
            FocusScenario.SendLine("name scratch report"),
            FocusScenario.WaitForOutput("File name set to \"scratch report\""),
            FocusScenario.SendLine("cancel"),
            FocusScenario.WaitForOutput("Navigate"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(context.ResolveExportPath("scratch report.html")));
        Assert.Equal("Initial commit", gitSandbox.GetRemoteHeadCommitMessage());
    }

    [Fact]
    public async Task GitSync_PersistedCommand_PushesExpectedCommitMessage()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        var map = new MindMap("Alpha root");
        map.AddAtCurrentNode("Child");
        gitSandbox.WriteWorkingMap("alpha", map);
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Child"),
            FocusScenario.SendLine("todo 1"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"));

        Assert.Equal("Mark task as todo in alpha", gitSandbox.GetRemoteHeadCommitMessage());
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", savedMap =>
            {
                var child = savedMap.GetNode("1") ?? throw new InvalidOperationException("Child node missing.");
                Assert.Equal(TaskState.Todo, child.TaskState);
            }));

        await FocusScenario.RunAsync(
            context,
            FocusScenario.SendLine("exit"));
    }

    [Fact]
    public async Task GitSync_SaveFailure_ShowsGenericError_AndKeepsCommitLocalOnly()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        var map = new MindMap("Alpha root");
        map.AddAtCurrentNode("Child");
        gitSandbox.WriteWorkingMap("alpha", map);
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Child"));

        gitSandbox.SetWorkingRemoteToMissingPath();

        await FocusScenario.RunAsync(
            context,
            FocusScenario.SendLine("min 1"),
            FocusScenario.WaitForOutput("Error occured while saving map changes."),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertGitCommit("Hide node in alpha", GitCommitTarget.Working),
            FocusScenario.AssertMap("alpha.json", savedMap =>
            {
                var child = savedMap.GetNode("1") ?? throw new InvalidOperationException("Child node missing.");
                Assert.True(child.Collapsed);
            }));
        Assert.Equal("Initial commit", gitSandbox.GetRemoteHeadCommitMessage());
    }

    [Fact]
    public async Task GitSync_RefreshFailure_ShowsGenericErrorMessage()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        gitSandbox.WriteWorkingMap("alpha", new MindMap("Alpha root"));
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.Pause(TimeSpan.FromSeconds(1)));

        gitSandbox.SetWorkingRemoteToMissingPath();

        await FocusScenario.RunAsync(
            context,
            FocusScenario.SendLine("ls"),
            FocusScenario.WaitForOutput("Error occured while refreshing maps from git."),
            FocusScenario.WaitForOutput("Press any key to continue"),
            FocusScenario.SendKey(new ConsoleKeyInfo('e', ConsoleKey.E, shift: false, alt: false, control: false)),
            FocusScenario.WaitForOutputOccurrences("Welcome", 2),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal("Initial commit", gitSandbox.GetRemoteHeadCommitMessage());
    }

    [Fact]
    public async Task System_ConfirmationCancels_KeepHomeAndMapStateUnchanged()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        var map = new MindMap("Alpha root");
        map.AddAtCurrentNode("Child");
        SaveMap(workspace, "alpha", map);
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("del 1"),
            FocusScenario.WaitForOutput("Are you sure you want to delete: \"alpha.json\"?"),
            FocusScenario.SendLine(string.Empty),
            FocusScenario.WaitForOutputOccurrences("Welcome", 2),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Child"),
            FocusScenario.SendLine("del 1"),
            FocusScenario.WaitForOutput("Are you sure to delete \"1\". \"Child\""),
            FocusScenario.SendLine(string.Empty),
            FocusScenario.WaitForOutput("Cancelled!"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutputOccurrences("Welcome", 3),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", savedMap =>
            {
                Assert.Equal("Alpha root", savedMap.RootNode.Name);
                Assert.Collection(
                    savedMap.RootNode.Children,
                    child => Assert.Equal("Child", child.Name));
            }));
    }

    [Fact]
    public async Task System_OpenFailure_ShowsGenericDialogAndReturnsHome()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        Directory.CreateDirectory(workspace.MapsDirectory);
        File.WriteAllText(Path.Combine(workspace.MapsDirectory, "broken.json"), "{ invalid json");
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("broken"),
            FocusScenario.WaitForOutput("Error occured while opening map."),
            FocusScenario.WaitForOutput("Press any key to continue"),
            FocusScenario.SendKey(new ConsoleKeyInfo('e', ConsoleKey.E, shift: false, alt: false, control: false)),
            FocusScenario.WaitForOutputOccurrences("Welcome", 2),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("invalid json", app.GetTranscript(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task System_OpenConflictedMap_AutoResolvesAndOpensEditor()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        Directory.CreateDirectory(workspace.MapsDirectory);
        var filePath = Path.Combine(workspace.MapsDirectory, "conflicted.json");
        File.WriteAllText(
            filePath,
            BuildWholeDocumentConflict(
                BuildConflictMapJson("Root ours", "2026-04-20T08:00:00Z"),
                BuildConflictMapJson("Root theirs", "2026-04-20T10:00:00Z")));
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("conflicted"),
            FocusScenario.WaitForOutput("Root theirs"),
            FocusScenario.WaitForOutput("Navigate"),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutputOccurrences("Welcome", 2),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("<<<<<<<", File.ReadAllText(filePath), StringComparison.Ordinal);
        Assert.DoesNotContain("Error occured while opening map.", app.GetTranscript(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task System_OpenConflictedMap_WhenAutoResolveFails_ShowsSpecificConflictMessage()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        Directory.CreateDirectory(workspace.MapsDirectory);
        File.WriteAllText(
            Path.Combine(workspace.MapsDirectory, "conflicted.json"),
            "<<<<<<< HEAD\nnot json\n=======\nstill not json\n>>>>>>> abc123");
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("conflicted"),
            FocusScenario.WaitForOutput(MapConflictAutoResolveException.DefaultMessage),
            FocusScenario.WaitForOutput("Press any key to continue"),
            FocusScenario.SendKey(new ConsoleKeyInfo('e', ConsoleKey.E, shift: false, alt: false, control: false)),
            FocusScenario.WaitForOutputOccurrences("Welcome", 2),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("Error occured while opening map.", app.GetTranscript(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task System_OpenAlreadyResolvedButStillUnmergedMap_FinalizesMergeAutomatically()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        gitSandbox.WriteWorkingMap("alpha", new MindMap("Alpha root"));
        gitSandbox.CommitAndPushWorking("Add alpha");
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"));

        gitSandbox.WriteWorkingMap("alpha", new MindMap("Alpha local"));
        gitSandbox.CommitWorking("Local alpha edit");
        gitSandbox.PullCollaborator();
        gitSandbox.WriteCollaboratorMap("alpha", new MindMap("Alpha remote"));
        gitSandbox.CommitAndPushCollaborator("Remote alpha edit");
        gitSandbox.PullWorkingExpectConflict();
        gitSandbox.WriteWorkingMap("alpha", new MindMap("Alpha resolved"));

        Assert.True(gitSandbox.HasWorkingMergeInProgress());
        Assert.Contains("FocusMaps/alpha.json", gitSandbox.GetWorkingUnmergedFiles());

        await FocusScenario.RunAsync(
            context,
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha resolved"),
            FocusScenario.WaitForOutputOccurrences("Commands hidden. Press \"~\" to show.", 2),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutputOccurrences("Welcome", 2),
            FocusScenario.SendLine("ls"),
            FocusScenario.WaitForOutputOccurrences("Welcome", 3),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.False(gitSandbox.HasWorkingMergeInProgress());
        Assert.Empty(gitSandbox.GetWorkingUnmergedFiles());
        Assert.DoesNotContain("Git merge still has unresolved files", app.GetTranscript(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task System_OpenAlreadyResolvedButStillUnmergedMap_WithSpacesAndNonAsciiFileName_FinalizesMergeAutomatically()
    {
        const string fileName = "საოჯახო map";

        var canonicalFileName = "\u10E1\u10D0\u10DD\u10EF\u10D0\u10EE\u10DD map";
        _ = fileName;

        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        gitSandbox.WriteWorkingMap(canonicalFileName, new MindMap("Alpha root"));
        gitSandbox.CommitAndPushWorking("Add alpha");
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.Pause(TimeSpan.FromSeconds(1)));

        gitSandbox.WriteWorkingMap(canonicalFileName, new MindMap("Alpha local"));
        gitSandbox.CommitWorking("Local alpha edit");
        gitSandbox.PullCollaborator();
        gitSandbox.WriteCollaboratorMap(canonicalFileName, new MindMap("Alpha remote"));
        gitSandbox.CommitAndPushCollaborator("Remote alpha edit");
        gitSandbox.PullWorkingExpectConflict();
        gitSandbox.WriteWorkingMap(canonicalFileName, new MindMap("Alpha resolved"));

        Assert.True(gitSandbox.HasWorkingMergeInProgress());
        Assert.Contains($"FocusMaps/{canonicalFileName}.json", gitSandbox.GetWorkingUnmergedFiles());

        await FocusScenario.RunAsync(
            context,
            FocusScenario.SendLine("1"),
            FocusScenario.WaitForOutput("Alpha resolved"),
            FocusScenario.WaitForOutputOccurrences("Commands hidden. Press \"~\" to show.", 2),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutputOccurrences("Welcome", 2),
            FocusScenario.SendLine("ls"),
            FocusScenario.WaitForOutputOccurrences("Welcome", 3),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.False(gitSandbox.HasWorkingMergeInProgress());
        Assert.Empty(gitSandbox.GetWorkingUnmergedFiles());
        Assert.DoesNotContain("Git merge still has unresolved files", app.GetTranscript(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task System_CaptureFailure_ShowsGenericErrorAndDoesNotCreateAttachment()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        SaveMap(workspace, "alpha", new MindMap("Alpha root"));
        await using var app = new FocusAppProcessHarness(
            workspace,
            new FocusAppLaunchOptions
            {
                ClipboardExceptionMessage = "clipboard service unavailable"
            });

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Alpha root"),
            FocusScenario.SendLine("capture"),
            FocusScenario.WaitForOutput("Error occured while capturing clipboard."),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("clipboard service unavailable", app.GetTranscript(), StringComparison.OrdinalIgnoreCase);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("alpha.json", savedMap =>
            {
                Assert.Empty(savedMap.RootNode.Metadata?.Attachments ?? []);
            }));
    }

    [Fact]
    public async Task System_UpdateCommand_IsHiddenWhenNoUpdateIsAvailable()
    {
        using var workspace = new FocusE2EWorkspace();
        workspace.WriteConfig();
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("update (", app.GetTranscript(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task System_StartupSync_EmitsUpdatesAvailableTitleWhenRepositoryChangesAtLaunch()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        gitSandbox.WriteWorkingMap("alpha", new MindMap("Alpha root"));
        gitSandbox.WriteCollaboratorMap("beta", new MindMap("Beta root"));
        gitSandbox.CommitAndPushCollaborator("Add beta before launch");
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);
        await using var app = new FocusAppProcessHarness(
            workspace,
            new FocusAppLaunchOptions
            {
                EmitTitles = true
            });

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.WaitForOutput("(updates available)"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("beta.json", savedMap => Assert.Equal("Beta root", savedMap.RootNode.Name)));
    }

    [Fact]
    public async Task ExportHtml_CreatesFile_AndPushesCommit()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        gitSandbox.WriteWorkingMap("alpha", new MindMap("alpha"));
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);

        await FocusScenario.RunAsync(
            context,
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("alpha"),
            FocusScenario.WaitForOutput("Navigate"),
            FocusScenario.SendLine("export"),
            FocusScenario.WaitForOutput("*** Export ***"),
            FocusScenario.SendLine("html"),
            FocusScenario.SendLine("save"),
            FocusScenario.WaitForOutput("Exported HTML to \"alpha.html\""),
            FocusScenario.SendLine("exit"),
            FocusScenario.WaitForOutput("Welcome"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertExportExists("alpha.html"),
            FocusScenario.AssertGitCommit("Export HTML from alpha", GitCommitTarget.Remote));
    }

    [Fact]
    public async Task Refresh_PullsRemoteMapFromCollaborator()
    {
        using var workspace = new FocusE2EWorkspace();
        using var gitSandbox = new GitSandbox(Path.Combine(workspace.RootDirectory, "git"));
        workspace.WriteConfig(gitSandbox.WorkingDirectory, gitSandbox.WorkingDirectory);
        await using var app = new FocusAppProcessHarness(workspace);

        await app.StartAsync();
        var context = new FocusScenarioContext(app, workspace, gitSandbox);
        await FocusScenario.RunAsync(context, FocusScenario.WaitForOutput("Welcome"));

        gitSandbox.WriteCollaboratorMap("remote-map", new MindMap("remote map"));
        gitSandbox.CommitAndPushCollaborator("Add remote map");

        await FocusScenario.RunAsync(
            context,
            FocusScenario.SendLine("ls"),
            FocusScenario.WaitForOutput("remote-map"),
            FocusScenario.SendLine("exit"));

        var exitCode = await app.WaitForExitAsync();

        Assert.Equal(0, exitCode);
        await FocusScenario.RunAsync(
            context,
            FocusScenario.AssertMap("remote-map.json", map => Assert.Equal("remote map", map.RootNode.Name)));
    }

    private static string SaveMap(FocusE2EWorkspace workspace, string fileName, MindMap map)
    {
        Directory.CreateDirectory(workspace.MapsDirectory);
        var filePath = Path.Combine(
            workspace.MapsDirectory,
            fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : $"{fileName}.json");
        MapFile.Save(filePath, map);
        return filePath;
    }

    private static string BuildWholeDocumentConflict(string ours, string theirs) =>
        $"<<<<<<< HEAD\n{ours}\n=======\n{theirs}\n>>>>>>> abc123";

    private static string BuildConflictMapJson(string rootName, string updatedAtUtc) =>
        $$"""
        {
          "rootNode": {
            "nodeType": 0,
            "uniqueIdentifier": "11111111-1111-1111-1111-111111111111",
            "name": "{{rootName}}",
            "children": [],
            "links": {},
            "number": 1,
            "collapsed": false,
            "hideDoneTasks": false,
            "taskState": 0,
            "metadata": {
              "createdAtUtc": "2026-04-20T07:00:00Z",
              "updatedAtUtc": "{{updatedAtUtc}}",
              "source": "manual",
              "device": "focus-pwa-web",
              "attachments": []
            }
          },
          "updatedAt": "{{updatedAtUtc}}"
        }
        """;

    private static void AddTextAttachment(Node node, string relativePath, string displayName)
    {
        node.Metadata ??= NodeMetadata.Create(NodeMetadataSources.Manual, device: null, timestampUtc: DateTimeOffset.UtcNow);
        node.Metadata.Attachments.Add(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = relativePath,
            MediaType = "text/plain; charset=utf-8",
            DisplayName = displayName,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static string WriteTextAttachment(string mapsDirectory, string mapFileName, string relativePath, string content)
    {
        var attachmentDirectory = Path.Combine(
            mapsDirectory,
            $"{Path.GetFileNameWithoutExtension(mapFileName)}{ConfigurationConstants.AttachmentDirectorySuffix}");
        Directory.CreateDirectory(attachmentDirectory);

        var attachmentPath = Path.Combine(attachmentDirectory, relativePath);
        File.WriteAllText(attachmentPath, content);
        return attachmentPath;
    }
}
