#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;

namespace Systems.Sanity.Focus.Application.HomeCommands;

internal sealed class HomeSystemCommandHandler : IHomeCommandFeatureHandler
{
    public IReadOnlyCollection<HomeCommandId> CommandIds { get; } = new[]
    {
        HomeCommandId.Exit,
        HomeCommandId.Refresh,
        HomeCommandId.UpdateApp
    };

    public HomeWorkflowResult Execute(
        HomeCommandContext context,
        HomeCommandId commandId,
        ConsoleInput input,
        IReadOnlyDictionary<int, FileInfo> fileSelection) =>
        commandId switch
        {
            HomeCommandId.Exit => HomeWorkflowResult.Exit,
            HomeCommandId.Refresh => HandleRefresh(context),
            HomeCommandId.UpdateApp => HandleUpdateApp(),
            _ => HomeWorkflowResult.Error($"Unsupported home command \"{commandId}\".")
        };

    private static HomeWorkflowResult HandleRefresh(HomeCommandContext context)
    {
        try
        {
            var result = context.AppContext.MapsStorage.PullLatestAtStartup();
            return result.Status == StartupSyncStatus.Failed
                ? HomeWorkflowResult.Error(result.ErrorMessage)
                : HomeWorkflowResult.Continue;
        }
        catch (Exception ex)
        {
            return HomeWorkflowResult.Error(ExceptionDiagnostics.LogException(ex, "refreshing maps"));
        }
    }

    private static HomeWorkflowResult HandleUpdateApp()
    {
        try
        {
            AutoUpdateManager.HandleUpdate();
            return HomeWorkflowResult.Continue;
        }
        catch (Exception ex)
        {
            return HomeWorkflowResult.Error(ExceptionDiagnostics.LogException(ex, "updating application"));
        }
    }
}
