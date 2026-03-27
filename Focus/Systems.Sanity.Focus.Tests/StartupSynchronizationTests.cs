using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
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
        Assert.Contains("git pull failed", result.ErrorMessage);
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
    public void CreateHomePage_ShowsStartupFailureBanner_OnlyOnce()
    {
        using var workspace = new TestWorkspace();
        var mapsStorage = CreateMapsStorage(
            workspace.RootDirectory,
            StartupSyncResult.Failed("git pull failed with exit code 1"));

        var homePage = ApplicationStartup.CreateHomePage(mapsStorage);
        var firstBanner = homePage.ConsumeInitialBannerText();
        var secondBanner = homePage.ConsumeInitialBannerText();

        Assert.Contains("Startup sync failed. Showing local maps.", firstBanner);
        Assert.Contains("git pull failed", firstBanner);
        Assert.True(string.IsNullOrWhiteSpace(secondBanner));
    }

    [Theory]
    [InlineData(StartupSyncStatus.Skipped)]
    [InlineData(StartupSyncStatus.Succeeded)]
    public void CreateHomePage_DoesNotShowStartupBanner_WhenSyncDidNotFail(StartupSyncStatus startupSyncStatus)
    {
        using var workspace = new TestWorkspace();
        var startupSyncResult = startupSyncStatus == StartupSyncStatus.Succeeded
            ? StartupSyncResult.Succeeded
            : StartupSyncResult.Skipped;
        var mapsStorage = CreateMapsStorage(workspace.RootDirectory, startupSyncResult);

        var homePage = ApplicationStartup.CreateHomePage(mapsStorage);

        Assert.True(string.IsNullOrWhiteSpace(homePage.ConsumeInitialBannerText()));
    }

    [Fact]
    public void CreateHomePage_InvokesStartupSyncBeforeReturning()
    {
        using var workspace = new TestWorkspace();
        var fileSynchronizationHandler = new RecordingFileSynchronizationHandler(StartupSyncResult.Succeeded);
        var mapsStorage = new MapsStorage(
            new UserConfig
            {
                DataFolder = workspace.RootDirectory,
                GitRepository = string.Empty
            },
            fileSynchronizationHandler);

        _ = ApplicationStartup.CreateHomePage(mapsStorage);

        Assert.Equal(1, fileSynchronizationHandler.PullLatestAtStartupCallCount);
    }

    private static MapsStorage CreateMapsStorage(string dataFolder, StartupSyncResult startupSyncResult)
    {
        return new MapsStorage(
            new UserConfig
            {
                DataFolder = dataFolder,
                GitRepository = string.Empty
            },
            new RecordingFileSynchronizationHandler(startupSyncResult));
    }

    private sealed class RecordingFileSynchronizationHandler : IFileSynchronizationHandler
    {
        private readonly StartupSyncResult _startupSyncResult;

        public RecordingFileSynchronizationHandler(StartupSyncResult startupSyncResult)
        {
            _startupSyncResult = startupSyncResult;
        }

        public int PullLatestAtStartupCallCount { get; private set; }

        public StartupSyncResult PullLatestAtStartup()
        {
            PullLatestAtStartupCallCount++;
            return _startupSyncResult;
        }

        public void Synchronize()
        {
        }
    }
}
