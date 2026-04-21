using Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization;

public class FileSynchronizationHandlerGit : IFileSynchronizationHandler
{
    private readonly GitHelper _gitHelper;

    public FileSynchronizationHandlerGit(GitHelper gitHelper)
    {
        _gitHelper = gitHelper;
    }

    public void Synchronize(string commitMessage) => _gitHelper.SynchronizeToRemote(commitMessage);

    public StartupSyncResult PullLatestAtStartup() => _gitHelper.PullLatestAtStartup();

    public MergeRecoveryResult TryRecoverResolvedFile(string absoluteFilePath) =>
        _gitHelper.TryRecoverResolvedFile(absoluteFilePath);
}
