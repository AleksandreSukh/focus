using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Systems.Sanity.Focus;

internal class AutoUpdateManager
{
    private static UpdateInfo updateInfo;
    private static readonly object _lockObj = new();
    private static UpdateManager _updateManager;

    public static async Task StartUpdateChecker()
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        var mgr = GetGithubUpdateManager();
        if (mgr.IsInstalled)
        {
            while (true)
            {
                await CheckForUpdate();
                await Task.Delay(TimeSpan.FromSeconds(100));
            }
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
        var mgr = GetGithubUpdateManager();
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

    private static UpdateManager GetGithubUpdateManager() => _updateManager ??= new UpdateManager(new GithubSource("https://github.com/AleksandreSukh/focus", null, false));

    private static async Task CheckForUpdate()
    {
        try
        {
            var mgr = GetGithubUpdateManager();
            if (mgr.IsInstalled)
            {
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
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}