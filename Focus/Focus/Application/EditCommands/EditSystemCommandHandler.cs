#nullable enable

using System.Collections.Generic;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditSystemCommandHandler : IEditCommandFeatureHandler
{
    public IReadOnlyCollection<EditCommandId> CommandIds { get; } = new[]
    {
        EditCommandId.Exit
    };

    public CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters) =>
        commandId switch
        {
            EditCommandId.Exit => CommandExecutionResult.ExitCommand,
            _ => CommandExecutionResult.Error($"Unsupported command \"{commandId}\"")
        };
}
