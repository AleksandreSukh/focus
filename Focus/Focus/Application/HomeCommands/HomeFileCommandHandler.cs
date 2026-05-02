#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;

namespace Systems.Sanity.Focus.Application.HomeCommands;

internal sealed class HomeFileCommandHandler : IHomeCommandFeatureHandler
{
    public IReadOnlyCollection<HomeCommandId> CommandIds { get; } = new[]
    {
        HomeCommandId.CreateFile,
        HomeCommandId.RenameFile,
        HomeCommandId.DeleteFile
    };

    public HomeWorkflowResult Execute(
        HomeCommandContext context,
        HomeCommandId commandId,
        ConsoleInput input,
        IReadOnlyDictionary<int, FileInfo> fileSelection) =>
        commandId switch
        {
            HomeCommandId.CreateFile => HandleCreateFile(context, input),
            HomeCommandId.RenameFile => HandleRenameFile(context, input, fileSelection),
            HomeCommandId.DeleteFile => HandleDeleteFile(context, input, fileSelection),
            _ => HomeWorkflowResult.Error($"Unsupported home command \"{commandId}\".")
        };

    private static HomeWorkflowResult HandleCreateFile(HomeCommandContext context, ConsoleInput input)
    {
        try
        {
            context.AppContext.Navigator.OpenCreateMap(input.Parameters, new MindMap(input.Parameters));
            return HomeWorkflowResult.Continue;
        }
        catch (Exception ex)
        {
            return HomeWorkflowResult.Error(ExceptionDiagnostics.LogException(ex, "creating map"));
        }
    }

    private static HomeWorkflowResult HandleDeleteFile(
        HomeCommandContext context,
        ConsoleInput input,
        IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        var file = ResolveFile(context, fileSelection, input.Parameters);
        if (file == null)
            return HomeWorkflowResult.Error($"File \"{input.Parameters}\" wasn't found. Try again.");

        try
        {
            if (context.AppContext.WorkflowInteractions.Confirm(
                    $"Are you sure you want to delete: \"{file.Name}\" and all of its attachments?"))
            {
                context.AppContext.MapRepository.DeleteMap(file, MapDeletionMode.DeleteAttachments);
                context.AppContext.RefreshLinkIndex();
            }

            return HomeWorkflowResult.Continue;
        }
        catch (MapDeletionBlockedException ex)
        {
            return HomeWorkflowResult.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return HomeWorkflowResult.Error(ExceptionDiagnostics.LogException(ex, "deleting map"));
        }
    }

    private static HomeWorkflowResult HandleRenameFile(
        HomeCommandContext context,
        ConsoleInput input,
        IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        var file = ResolveFile(context, fileSelection, input.Parameters);
        if (file == null)
            return HomeWorkflowResult.Error($"File \"{input.Parameters}\" wasn't found. Try again.");

        try
        {
            var newFilePath = context.AppContext.WorkflowInteractions.RenameMapFile(context.AppContext.MapRepository, file);
            if (newFilePath != null)
            {
                var newBaseName = Path.GetFileNameWithoutExtension(newFilePath);
                var map = context.AppContext.MapRepository.OpenMap(newFilePath);
                map.RootNode.EditNode(newBaseName);
                context.AppContext.MapRepository.SaveMap(newFilePath, map);
                context.AppContext.RefreshLinkIndex();
            }

            return HomeWorkflowResult.Continue;
        }
        catch (Exception ex)
        {
            return HomeWorkflowResult.Error(ExceptionDiagnostics.LogException(ex, "renaming map"));
        }
    }

    private static FileInfo? ResolveFile(
        HomeCommandContext context,
        IReadOnlyDictionary<int, FileInfo> fileSelection,
        string fileIdentifier) =>
        context.AppContext.MapSelectionService.FindFile(fileSelection, fileIdentifier);
}
