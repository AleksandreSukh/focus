#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;

namespace Systems.Sanity.Focus.Application.HomeCommands;

internal sealed class HomeFileOpenFallbackProcessor
{
    public HomeWorkflowResult Execute(
        HomeCommandContext context,
        ConsoleInput input,
        IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        var file = context.AppContext.MapSelectionService.FindFile(fileSelection, input.FirstWord);
        if (file == null)
            return HomeWorkflowResult.Error($"File \"{input.FirstWord}\" wasn't found. Try again.");

        try
        {
            context.AppContext.Navigator.OpenEditMap(file.FullName);
            return HomeWorkflowResult.Continue;
        }
        catch (MapConflictAutoResolveException ex)
        {
            return HomeWorkflowResult.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return HomeWorkflowResult.Error(ExceptionDiagnostics.LogException(ex, "opening map"));
        }
    }
}
