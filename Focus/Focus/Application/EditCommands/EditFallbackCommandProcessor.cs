#nullable enable

using System.Linq;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditFallbackCommandProcessor
{
    public CommandExecutionResult Execute(EditCommandContext context, string command, ConsoleInput input)
    {
        if (!context.Map.GetChildren().Any())
        {
            if (EditAttachmentOperations.TryOpenCurrentAttachmentShortcut(context, command, out var directAttachmentResult))
                return directAttachmentResult;

            return ProcessAddCurrentInputString(context, input);
        }

        var goToChildCommandResult = ProcessCommandGoToChild(context, command);
        if (goToChildCommandResult.IsSuccess)
            return goToChildCommandResult;

        if (EditAttachmentOperations.TryOpenCurrentAttachmentShortcut(context, command, out var attachmentResult))
            return attachmentResult;

        return context.AppContext.WorkflowInteractions.Confirm(
                $"Did you mean to add new record? \"{NodeDisplayHelper.GetContentPeek(input.InputString)}\"")
            ? ProcessAddCurrentInputString(context, input)
            : goToChildCommandResult;
    }

    private static CommandExecutionResult ProcessAddCurrentInputString(EditCommandContext context, ConsoleInput input)
    {
        return context.AppContext.WorkflowInteractions.AddNotes(context.Map, input.InputString)
            ? context.PersistMapChange("Add note")
            : CommandExecutionResult.Success();
    }

    private static CommandExecutionResult ProcessCommandGoToChild(EditCommandContext context, string parameters)
    {
        return EditNodeIdentifier.InvokeLocalized(context.Map.ChangeCurrentNode, parameters)
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.Error($"Can't find \"{parameters}\"");
    }
}
