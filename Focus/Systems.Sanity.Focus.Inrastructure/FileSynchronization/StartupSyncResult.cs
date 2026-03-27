namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization;

public enum StartupSyncStatus
{
    Skipped,
    Succeeded,
    Failed
}

public readonly record struct StartupSyncResult(StartupSyncStatus Status, string ErrorMessage = "")
{
    public static StartupSyncResult Skipped { get; } = new(StartupSyncStatus.Skipped);

    public static StartupSyncResult Succeeded { get; } = new(StartupSyncStatus.Succeeded);

    public static StartupSyncResult Failed(string errorMessage) => new(StartupSyncStatus.Failed, errorMessage);
}
