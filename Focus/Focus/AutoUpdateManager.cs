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
        var githubUpdateManager = GetGithubUpdateManager();
        if (githubUpdateManager.IsInstalled)
        {
            while (true)
            {
                await CheckForUpdate();
                await Task.Delay(TimeSpan.FromMinutes(30));
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
        var githubUpdateManager = GetGithubUpdateManager();
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
            githubUpdateManager.DownloadUpdates(versionToUpdateTo);

            githubUpdateManager.ApplyUpdatesAndRestart(versionToUpdateTo);
        }
    }

    private static UpdateManager GetGithubUpdateManager() => _updateManager ??= 
        new UpdateManager(new SimpleWebSource("https://focusupdate.sandro.casa/Releases"));

    private static async Task CheckForUpdate()
    {
        try
        {
            var githubUpdateManager = GetGithubUpdateManager();
            if (githubUpdateManager.IsInstalled)
            {
                // check for new version
                var newVersion = await githubUpdateManager.CheckForUpdatesAsync();
                if (newVersion != null)
                {
                    Console.Title = $"Update available:\"{newVersion.TargetFullRelease.Version}\"";

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