namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization;

public class FileSynchronizationHandlerEmpty : IFileSynchronizationHandler
{
    public void Synchronize(string commitMessage)
    {
        // Git synchronization is unavailable for the current configuration.
    }

    public StartupSyncResult PullLatestAtStartup()
    {
        return StartupSyncResult.Skipped;
    }
}
