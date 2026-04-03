using System.Diagnostics;
using System.IO;
using System.Threading;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

namespace Systems.Sanity.Focus.Tests;

public class StartupSynchronizationTests
{
    [Fact]
    public void PullLatestAtStartup_Skips_WhenGitSynchronizationIsUnavailable()
    {
        using var workspace = new TestWorkspace();
        var mapsStorage = new MapsStorage(
            new UserConfig
            {
                DataFolder = workspace.RootDirectory,
                GitRepository = string.Empty
            });

        var result = mapsStorage.PullLatestAtStartup();

        Assert.Equal(StartupSyncStatus.Skipped, result.Status);
        Assert.True(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void PullLatestAtStartup_ReturnsFailure_WhenPullCommandFails()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        var gitHelper = new GitHelper(
            gitRepositoryPath: @"C:\repo",
            executeGitCommand: (_, _, _, arguments) =>
            {
                if (arguments.SequenceEqual(["pull", "--no-rebase", "--quiet"]))
                    throw new InvalidOperationException("git pull failed with exit code 1");

                return (0, string.Empty, string.Empty);
            });

        var result = gitHelper.PullLatestAtStartup();

        Assert.Equal(StartupSyncStatus.Failed, result.Status);
        Assert.Equal(ExceptionDiagnostics.BuildUserMessage("refreshing maps from git"), result.ErrorMessage);
        Assert.Contains("Action: refreshing maps from git", diagnosticsScope.ReadLog());
        Assert.Contains("git pull failed with exit code 1", diagnosticsScope.ReadLog());
    }

    [Fact]
    public void PullLatestAtStartup_OnlyRunsPullCommand()
    {
        var executedCommands = new List<string[]>();
        var gitHelper = new GitHelper(
            gitRepositoryPath: @"C:\repo",
            executeGitCommand: (_, _, _, arguments) =>
            {
                executedCommands.Add(arguments);
                return (0, string.Empty, string.Empty);
            });

        var result = gitHelper.PullLatestAtStartup();

        Assert.Equal(StartupSyncStatus.Succeeded, result.Status);
        Assert.Single(executedCommands);
        Assert.Equal(["pull", "--no-rebase", "--quiet"], executedCommands[0]);
    }

    [Fact]
    public void CreateHomePage_DoesNotWaitForStartupSyncToFinish()
    {
        using var workspace = new TestWorkspace();
        using var releasePull = new ManualResetEventSlim(false);
        using var pullStarted = new ManualResetEventSlim(false);
        var mapsStorage = CreateMapsStorage(
            workspace.RootDirectory,
            () =>
            {
                pullStarted.Set();
                releasePull.Wait(TimeSpan.FromSeconds(5));
                return StartupSyncResult.Succeeded;
            });

        var stopwatch = Stopwatch.StartNew();
        var homePage = ApplicationStartup.CreateHomePage(mapsStorage);
        stopwatch.Stop();

        Assert.NotNull(homePage);
        Assert.True(pullStarted.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));

        releasePull.Set();
    }

    [Fact]
    public async Task StartStartupSyncInBackground_MarksCurrentFileForReload_WhenCurrentFileChanges()
    {
        using var workspace = new TestWorkspace();
        var changedFilePath = workspace.SaveMap("alpha", new MindMap("Alpha"));
        var mapsStorage = CreateMapsStorage(
            workspace.RootDirectory,
            () =>
            {
                File.AppendAllText(changedFilePath, Environment.NewLine);
                return StartupSyncResult.Succeeded;
            });
        var appContext = new FocusAppContext(mapsStorage);
        appContext.StartupSyncNotificationState.SetCurrentOpenFile(changedFilePath);

        await ApplicationStartup.StartStartupSyncInBackground(appContext);

        Assert.Equal(
            $"{Path.GetFileName(changedFilePath)} (update required)",
            appContext.StartupSyncNotificationState.GetCurrentTitle());
        Assert.True(appContext.StartupSyncNotificationState.TryConsumeCurrentFileUpdateWarning(
            changedFilePath,
            out var warningMessage));
        Assert.Contains("Return to HomePage and reopen it", warningMessage);
        Assert.False(appContext.StartupSyncNotificationState.TryConsumeCurrentFileUpdateWarning(
            changedFilePath,
            out _));
    }

    [Fact]
    public async Task StartStartupSyncInBackground_SetsUpdatesAvailableTitle_WhenDifferentFileChanges()
    {
        using var workspace = new TestWorkspace();
        var openFilePath = workspace.SaveMap("alpha", new MindMap("Alpha"));
        var changedFilePath = workspace.SaveMap("beta", new MindMap("Beta"));
        var mapsStorage = CreateMapsStorage(
            workspace.RootDirectory,
            () =>
            {
                File.AppendAllText(changedFilePath, Environment.NewLine);
                return StartupSyncResult.Succeeded;
            });
        var appContext = new FocusAppContext(mapsStorage);
        appContext.StartupSyncNotificationState.SetCurrentOpenFile(openFilePath);

        await ApplicationStartup.StartStartupSyncInBackground(appContext);

        Assert.Equal(
            $"{Path.GetFileName(openFilePath)} (updates available)",
            appContext.StartupSyncNotificationState.GetCurrentTitle());
        Assert.False(appContext.StartupSyncNotificationState.TryConsumeCurrentFileUpdateWarning(
            openFilePath,
            out _));
    }

    [Fact]
    public async Task StartStartupSyncInBackground_WritesGenericBackgroundMessage_WhenSyncThrowsUnexpectedly()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        var consoleSession = new ScriptedConsoleSession();
        using var consoleScope = new AppConsoleScope(consoleSession);
        var syncHandler = new ThrowingFileSynchronizationHandler
        {
            PullLatestAtStartupException = new InvalidOperationException("startup sync exploded")
        };
        using var workspace = new TestWorkspace(fileSynchronizationHandler: syncHandler);

        await ApplicationStartup.StartStartupSyncInBackground(workspace.AppContext);

        Assert.Contains(
            ExceptionDiagnostics.BuildUserMessage("running startup sync"),
            consoleSession.BackgroundMessages);
        Assert.Contains("Action: running startup sync", diagnosticsScope.ReadLog());
        Assert.Contains("startup sync exploded", diagnosticsScope.ReadLog());
    }

    [Fact]
    public void HomePageRefresh_AcknowledgesPendingUpdates()
    {
        var notificationState = new StartupSyncNotificationState();
        notificationState.ApplyRepositoryUpdates([@"C:\maps\alpha.json"]);

        Assert.Equal("Welcome (updates available)", notificationState.BuildTitle("Welcome"));

        notificationState.AcknowledgeHomePageRefresh();

        Assert.Equal("Welcome", notificationState.BuildTitle("Welcome"));
    }

    [Fact]
    public void GetCurrentTitle_UsesApplicationTitle_WhenNoFileIsOpen()
    {
        var notificationState = new StartupSyncNotificationState();

        Assert.Equal(ApplicationInfo.DefaultConsoleTitle, notificationState.GetCurrentTitle());
    }

    [Fact]
    public void GetCurrentTitle_UsesApplicationTitleWithUpdateSuffix_WhenNoFileIsOpenAndUpdatesAreAvailable()
    {
        var notificationState = new StartupSyncNotificationState();
        notificationState.ApplyRepositoryUpdates([@"C:\maps\alpha.json"]);

        Assert.Equal(
            $"{ApplicationInfo.DefaultConsoleTitle} (updates available)",
            notificationState.GetCurrentTitle());
    }

    private static MapsStorage CreateMapsStorage(string dataFolder, Func<StartupSyncResult> pullLatestAtStartup)
    {
        return new MapsStorage(
            new UserConfig
            {
                DataFolder = dataFolder,
                GitRepository = string.Empty
            },
            new RecordingFileSynchronizationHandler(pullLatestAtStartup));
    }

    private sealed class RecordingFileSynchronizationHandler : IFileSynchronizationHandler
    {
        private readonly Func<StartupSyncResult> _pullLatestAtStartup;

        public RecordingFileSynchronizationHandler(Func<StartupSyncResult> pullLatestAtStartup)
        {
            _pullLatestAtStartup = pullLatestAtStartup;
        }

        public StartupSyncResult PullLatestAtStartup()
        {
            return _pullLatestAtStartup();
        }

        public void Synchronize(string commitMessage)
        {
        }
    }
}
