using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Systems.Sanity.Focus;

internal class AutoUpdateManager
{
    private static UpdateInfo updateInfo;
    private static readonly object _lockObj = new();

    public static async Task StartUpdateChecker()
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        while (true)
        {
            await CheckForUpdate();
            await Task.Delay(TimeSpan.FromSeconds(100));
        }
    }

    public static string CheckUpdatedVersion()
    {
        lock (_lockObj)
        {
            if (updateInfo != null)
                return updateInfo.TargetFullRelease.Version.ToFullString();
            return null;
        }
    }

    public static void HandleUpdate()
    {
        var mgr = GithubManagerFactory();
        UpdateInfo versionToUpdateTo = null;
        lock (_lockObj)
        {
            if (updateInfo != null)
            {
                versionToUpdateTo = new UpdateInfo(updateInfo.TargetFullRelease, updateInfo.IsDowngrade,
                    updateInfo.BaseRelease, updateInfo.DeltasToTarget);
            }
        }

        if (versionToUpdateTo != null)
        {
            mgr.DownloadUpdates(versionToUpdateTo);

            mgr.ApplyUpdatesAndRestart(versionToUpdateTo);
        }
    }

    private static UpdateManager GithubManagerFactory() => new UpdateManager(new GithubSource("https://github.com/AleksandreSukh/focus", null, false));

    private static async Task CheckForUpdate()
    {
        try
        {
            var mgr = GithubManagerFactory();
            // check for new version
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion != null)
            {
                Console.WriteLine("Found update " + newVersion.TargetFullRelease.Version);

                lock (_lockObj)
                {
                    updateInfo = newVersion;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}