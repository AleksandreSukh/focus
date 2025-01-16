using Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization;

public class FileSynchronizationHandlerGit : IFileSynchronizationHandler
{
    private readonly GitHelper _gitHelper;

    public FileSynchronizationHandlerGit(GitHelper gitHelper)
    {
        _gitHelper = gitHelper;
    }

    public void Synchronize() => _gitHelper.SyncronizeToRemote();
}