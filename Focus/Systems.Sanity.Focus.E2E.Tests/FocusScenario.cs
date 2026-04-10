using System;
using System.IO;
using System.Threading.Tasks;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.E2E.Tests;

internal sealed class FocusScenarioContext
{
    public FocusScenarioContext(FocusAppProcessHarness app, FocusE2EWorkspace workspace, GitSandbox? gitSandbox = null)
    {
        App = app;
        Workspace = workspace;
        GitSandbox = gitSandbox;
    }

    public FocusAppProcessHarness App { get; }

    public FocusE2EWorkspace Workspace { get; }

    public GitSandbox? GitSandbox { get; }

    public string ResolveMapPath(string fileName)
    {
        var normalizedFileName = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.json";
        var mapsDirectory = GitSandbox?.WorkingMapsDirectory ?? Workspace.MapsDirectory;
        return Path.Combine(mapsDirectory, normalizedFileName);
    }

    public string ResolveExportPath(string fileName)
    {
        var mapsDirectory = GitSandbox?.WorkingMapsDirectory ?? Workspace.MapsDirectory;
        return Path.Combine(mapsDirectory, fileName);
    }
}

internal interface IFocusScenarioStep
{
    Task ExecuteAsync(FocusScenarioContext context);
}

internal enum GitCommitTarget
{
    Working,
    Remote,
    Collaborator
}

internal static class FocusScenario
{
    public static async Task RunAsync(FocusScenarioContext context, params IFocusScenarioStep[] steps)
    {
        foreach (var step in steps)
            await step.ExecuteAsync(context);
    }

    public static IFocusScenarioStep SendLine(string text, int echoDelayMs = 0) =>
        new SendLineStep(text, echoDelayMs);

    public static IFocusScenarioStep SendKey(ConsoleKeyInfo keyInfo) =>
        new SendKeyStep(keyInfo);

    public static IFocusScenarioStep Pause(TimeSpan duration) =>
        new PauseStep(duration);

    public static IFocusScenarioStep WaitForOutput(string expectedText, TimeSpan? timeout = null) =>
        new WaitForOutputStep(expectedText, timeout);

    public static IFocusScenarioStep WaitForOutputOccurrences(string expectedText, int occurrences, TimeSpan? timeout = null) =>
        new WaitForOutputOccurrencesStep(expectedText, occurrences, timeout);

    public static IFocusScenarioStep AssertMap(string fileName, Action<MindMap> assertMap) =>
        new AssertMapStep(fileName, assertMap);

    public static IFocusScenarioStep AssertTaskState(string fileName, string nodeIdentifier, TaskState expectedTaskState) =>
        new AssertTaskStateStep(fileName, nodeIdentifier, expectedTaskState);

    public static IFocusScenarioStep AssertExportExists(string fileName) =>
        new AssertExportExistsStep(fileName);

    public static IFocusScenarioStep AssertGitCommit(string expectedCommitMessage, GitCommitTarget target = GitCommitTarget.Remote) =>
        new AssertGitCommitStep(expectedCommitMessage, target);

    private sealed record SendLineStep(string Text, int EchoDelayMs) : IFocusScenarioStep
    {
        public Task ExecuteAsync(FocusScenarioContext context) => context.App.SendLineAsync(Text, EchoDelayMs);
    }

    private sealed record SendKeyStep(ConsoleKeyInfo KeyInfo) : IFocusScenarioStep
    {
        public Task ExecuteAsync(FocusScenarioContext context) => context.App.SendKeyAsync(KeyInfo);
    }

    private sealed record PauseStep(TimeSpan Duration) : IFocusScenarioStep
    {
        public Task ExecuteAsync(FocusScenarioContext context) => Task.Delay(Duration);
    }

    private sealed record WaitForOutputStep(string ExpectedText, TimeSpan? Timeout) : IFocusScenarioStep
    {
        public Task ExecuteAsync(FocusScenarioContext context) => context.App.WaitForOutputAsync(ExpectedText, Timeout);
    }

    private sealed record WaitForOutputOccurrencesStep(string ExpectedText, int Occurrences, TimeSpan? Timeout)
        : IFocusScenarioStep
    {
        public Task ExecuteAsync(FocusScenarioContext context) =>
            context.App.WaitForOutputOccurrencesAsync(ExpectedText, Occurrences, Timeout);
    }

    private sealed record AssertMapStep(string FileName, Action<MindMap> AssertMap) : IFocusScenarioStep
    {
        public Task ExecuteAsync(FocusScenarioContext context)
        {
            var map = MapFile.OpenFile(context.ResolveMapPath(FileName));
            AssertMap(map);
            return Task.CompletedTask;
        }
    }

    private sealed record AssertTaskStateStep(string FileName, string NodeIdentifier, TaskState ExpectedTaskState)
        : IFocusScenarioStep
    {
        public Task ExecuteAsync(FocusScenarioContext context)
        {
            var map = MapFile.OpenFile(context.ResolveMapPath(FileName));
            Assert.Equal(ExpectedTaskState, map.GetNode(NodeIdentifier)?.TaskState);
            return Task.CompletedTask;
        }
    }

    private sealed record AssertExportExistsStep(string FileName) : IFocusScenarioStep
    {
        public Task ExecuteAsync(FocusScenarioContext context)
        {
            Assert.True(File.Exists(context.ResolveExportPath(FileName)));
            return Task.CompletedTask;
        }
    }

    private sealed record AssertGitCommitStep(string ExpectedCommitMessage, GitCommitTarget Target) : IFocusScenarioStep
    {
        public Task ExecuteAsync(FocusScenarioContext context)
        {
            var gitSandbox = context.GitSandbox
                ?? throw new InvalidOperationException("Git sandbox is required for commit assertions.");
            var actualMessage = Target switch
            {
                GitCommitTarget.Working => gitSandbox.GetWorkingHeadCommitMessage(),
                GitCommitTarget.Remote => gitSandbox.GetRemoteHeadCommitMessage(),
                GitCommitTarget.Collaborator => gitSandbox.GetCollaboratorHeadCommitMessage(),
                _ => throw new ArgumentOutOfRangeException()
            };

            Assert.Equal(ExpectedCommitMessage, actualMessage);
            return Task.CompletedTask;
        }
    }
}
