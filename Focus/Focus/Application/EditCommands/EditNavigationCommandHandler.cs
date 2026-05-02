#nullable enable

using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditNavigationCommandHandler : IEditCommandFeatureHandler
{
    private const string GoToOptionSubOptionUp = "..";

    public IReadOnlyCollection<EditCommandId> CommandIds { get; } = new[]
    {
        EditCommandId.GoTo,
        EditCommandId.GoToRoot,
        EditCommandId.GoUp
    };

    public CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters) =>
        commandId switch
        {
            EditCommandId.GoTo => ProcessGoTo(context, parameters),
            EditCommandId.GoToRoot => ProcessGoToRoot(context),
            EditCommandId.GoUp => ProcessCommandGoUp(context),
            _ => CommandExecutionResult.Error($"Unsupported command \"{commandId}\"")
        };

    private static CommandExecutionResult ProcessGoTo(EditCommandContext context, string parameters)
    {
        return parameters == GoToOptionSubOptionUp
            ? ProcessCommandGoUp(context)
            : ProcessCommandGoToChild(context, parameters);
    }

    private static CommandExecutionResult ProcessCommandGoToChild(EditCommandContext context, string parameters)
    {
        return EditNodeIdentifier.InvokeLocalized(context.Map.ChangeCurrentNode, parameters)
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.Error($"Can't find \"{parameters}\"");
    }

    private static CommandExecutionResult ProcessCommandGoUp(EditCommandContext context)
    {
        return context.Map.GoUp()
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.ExitCommand;
    }

    private static CommandExecutionResult ProcessGoToRoot(EditCommandContext context)
    {
        var reloadErrorMessage = PullAndReloadIfChanged(context);
        if (!string.IsNullOrWhiteSpace(reloadErrorMessage))
            return CommandExecutionResult.Error(reloadErrorMessage);

        return context.Map.GoToRoot()
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.Error("Can't go to root");
    }

    private static string? PullAndReloadIfChanged(EditCommandContext context)
    {
        try
        {
            var fileInfo = new FileInfo(context.FilePath);
            if (!fileInfo.Exists)
                return null;

            var beforeWrite = fileInfo.LastWriteTimeUtc;
            var beforeLength = fileInfo.Length;

            var result = context.AppContext.MapsStorage.PullLatestAtStartup();
            if (result.Status != StartupSyncStatus.Succeeded)
                return null;

            fileInfo.Refresh();
            if (fileInfo.LastWriteTimeUtc != beforeWrite || fileInfo.Length != beforeLength)
            {
                context.Map = context.AppContext.MapRepository.OpenMapForEditing(context.FilePath);
                context.AppContext.RefreshLinkIndex();
            }

            return null;
        }
        catch (MapConflictAutoResolveException ex)
        {
            return ex.Message;
        }
        catch
        {
            return null;
        }
    }
}
