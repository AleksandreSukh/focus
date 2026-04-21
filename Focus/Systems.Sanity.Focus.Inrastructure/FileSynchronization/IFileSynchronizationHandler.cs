namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization;

public interface IFileSynchronizationHandler
{
    void Synchronize(string commitMessage);

    StartupSyncResult PullLatestAtStartup();

    MergeRecoveryResult TryRecoverResolvedFile(string absoluteFilePath);
}
