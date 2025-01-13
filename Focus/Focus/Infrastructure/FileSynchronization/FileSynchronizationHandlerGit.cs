using System;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization;

internal class FileSynchronizationHandlerGit : IFileSynchronizationHandler
{
    private readonly GitHelper _gitHelper;

    internal FileSynchronizationHandlerGit(GitHelper gitHelper)
    {
        _gitHelper = gitHelper;
    }

    public void Synchronize()
    {
        try
        {
            _gitHelper.SyncronizeToRemote();
        }
        catch (Exception e)
        {
            new Notification($"Failed Git auto synchronization.{Environment.NewLine}Consider manually committing changes to the repository.{Environment.NewLine} Error:{Environment.NewLine}{e}")
                .Show();
        }
    }
}