using System;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;
using Systems.Sanity.Focus.Pages;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;

namespace Systems.Sanity.Focus.Application;

internal static class ApplicationStartup
{
    public static HomePage CreateHomePage(UserConfig userConfig)
    {
        return CreateHomePage(userConfig, new AppRuntimeOptions());
    }

    internal static HomePage CreateHomePage(UserConfig userConfig, AppRuntimeOptions runtimeOptions)
    {
        var mapsStorage = new MapsStorage(userConfig, runtimeOptions.GitSynchronizationOptions);
        var appContext = runtimeOptions.IsTestHost
            ? TestHostAppOverrides.CreateContext(mapsStorage, userConfig)
            : new FocusAppContext(
                mapsStorage,
                navigator: null,
                voiceRecorder: VoiceRecorderFactory.CreateDefault(userConfig.VoiceRecorder));
        return CreateHomePage(appContext);
    }

    internal static HomePage CreateHomePage(MapsStorage mapsStorage)
    {
        var appContext = new FocusAppContext(mapsStorage);
        return CreateHomePage(appContext);
    }

    private static HomePage CreateHomePage(FocusAppContext appContext)
    {
        _ = StartStartupSyncInBackground(appContext);
        return new HomePage(appContext);
    }

    internal static Task StartStartupSyncInBackground(FocusAppContext appContext)
    {
        var initialSnapshot = CaptureMapSnapshot(appContext.MapsStorage);
        return Task.Run(() => ExceptionDiagnostics.Guard(
            "running startup sync",
            () => RunStartupSync(appContext, initialSnapshot),
            message => AppConsole.Current.WriteBackgroundMessage(message)));
    }

    internal static IReadOnlyCollection<string> DetectChangedMapFiles(
        IReadOnlyDictionary<string, MapFileSnapshot> initialSnapshot,
        IReadOnlyDictionary<string, MapFileSnapshot> currentSnapshot)
    {
        var changedFiles = new List<string>();

        foreach (var filePath in initialSnapshot.Keys.Union(currentSnapshot.Keys, StringComparer.OrdinalIgnoreCase))
        {
            if (!initialSnapshot.TryGetValue(filePath, out var initialFile) ||
                !currentSnapshot.TryGetValue(filePath, out var currentFile) ||
                initialFile != currentFile)
            {
                changedFiles.Add(filePath);
            }
        }

        return changedFiles;
    }

    private static void RunStartupSync(
        FocusAppContext appContext,
        IReadOnlyDictionary<string, MapFileSnapshot> initialSnapshot)
    {
        var startupSyncResult = appContext.MapsStorage.PullLatestAtStartup();
        if (startupSyncResult.Status != StartupSyncStatus.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(startupSyncResult.ErrorMessage))
            {
                AppConsole.Current.WriteBackgroundMessage(startupSyncResult.ErrorMessage);
            }
            return;
        }

        var currentSnapshot = CaptureMapSnapshot(appContext.MapsStorage);
        var changedFiles = DetectChangedMapFiles(initialSnapshot, currentSnapshot);
        if (changedFiles.Count == 0)
            return;

        appContext.StartupSyncNotificationState.ApplyRepositoryUpdates(changedFiles);
        AppConsole.Current.SetTitle(appContext.StartupSyncNotificationState.GetCurrentTitle());
    }

    private static IReadOnlyDictionary<string, MapFileSnapshot> CaptureMapSnapshot(MapsStorage mapsStorage)
    {
        return mapsStorage.GetAll()
            .ToDictionary(
                file => Path.GetFullPath(file.FullName),
                file => new MapFileSnapshot(file.LastWriteTimeUtc, file.Length),
                StringComparer.OrdinalIgnoreCase);
    }

    internal readonly record struct MapFileSnapshot(DateTime LastWriteTimeUtc, long Length);
}
