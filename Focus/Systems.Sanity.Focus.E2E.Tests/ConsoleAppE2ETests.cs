using System;
using System.IO;
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
}
