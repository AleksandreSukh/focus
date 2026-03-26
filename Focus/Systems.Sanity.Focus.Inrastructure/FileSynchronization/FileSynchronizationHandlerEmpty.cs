namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization;

public class FileSynchronizationHandlerEmpty : IFileSynchronizationHandler
{
    public void Synchronize()
    {
        // Git synchronization is unavailable for the current configuration.
    }
}
