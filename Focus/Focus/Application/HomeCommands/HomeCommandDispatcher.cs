#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.HomeCommands;

internal sealed class HomeCommandDispatcher : IHomeCommandHandler
{
    private readonly Dictionary<HomeCommandId, IHomeCommandFeatureHandler> _handlersByCommand = new();

    public HomeCommandDispatcher(IEnumerable<IHomeCommandFeatureHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            foreach (var commandId in handler.CommandIds)
            {
                if (_handlersByCommand.ContainsKey(commandId))
                    throw new InvalidOperationException($"Duplicate home command handler for \"{commandId}\".");

                _handlersByCommand[commandId] = handler;
            }
        }
    }

    public bool CanHandle(HomeCommandId commandId) =>
        _handlersByCommand.ContainsKey(commandId);

    public HomeWorkflowResult Execute(
        HomeCommandContext context,
        HomeCommandId commandId,
        ConsoleInput input,
        IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        return _handlersByCommand.TryGetValue(commandId, out var handler)
            ? handler.Execute(context, commandId, input, fileSelection)
            : HomeWorkflowResult.Error($"Unsupported home command \"{commandId}\".");
    }
}
