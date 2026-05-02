#nullable enable

using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditCommandDispatcher : IEditCommandHandler
{
    private readonly Dictionary<EditCommandId, IEditCommandFeatureHandler> _handlersByCommand = new();

    public EditCommandDispatcher(IEnumerable<IEditCommandFeatureHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            foreach (var commandId in handler.CommandIds)
            {
                if (_handlersByCommand.ContainsKey(commandId))
                    throw new InvalidOperationException($"Duplicate edit command handler for \"{commandId}\".");

                _handlersByCommand[commandId] = handler;
            }
        }
    }

    public bool CanHandle(EditCommandId commandId) =>
        _handlersByCommand.ContainsKey(commandId);

    public CommandExecutionResult Execute(
        EditCommandContext context,
        EditCommandId commandId,
        string parameters)
    {
        return _handlersByCommand.TryGetValue(commandId, out var handler)
            ? handler.Execute(context, commandId, parameters)
            : CommandExecutionResult.Error($"Unsupported command \"{commandId}\"");
    }
}
