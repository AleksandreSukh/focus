#nullable enable

using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal interface IEditCommandHandler
{
    CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters);
}
