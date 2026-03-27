using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;
using Systems.Sanity.Focus.Pages;

namespace Systems.Sanity.Focus.Application;

internal static class ApplicationStartup
{
    public static HomePage CreateHomePage(UserConfig userConfig)
    {
        return CreateHomePage(new MapsStorage(userConfig));
    }

    internal static HomePage CreateHomePage(MapsStorage mapsStorage)
    {
        var startupSyncResult = mapsStorage.PullLatestAtStartup();
        var appContext = new FocusAppContext(mapsStorage);
        var startupMessage = BuildStartupMessage(startupSyncResult);

        return new HomePage(
            appContext,
            startupMessage,
            startupSyncResult.Status == StartupSyncStatus.Failed);
    }

    internal static string BuildStartupMessage(StartupSyncResult startupSyncResult)
    {
        if (startupSyncResult.Status != StartupSyncStatus.Failed)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(startupSyncResult.ErrorMessage))
            return "Startup sync failed. Showing local maps.";

        return $"Startup sync failed. Showing local maps. {startupSyncResult.ErrorMessage}";
    }
}
