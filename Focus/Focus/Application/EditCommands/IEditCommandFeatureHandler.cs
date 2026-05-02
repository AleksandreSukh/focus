#nullable enable

using System.Collections.Generic;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal interface IEditCommandFeatureHandler
{
    IReadOnlyCollection<EditCommandId> CommandIds { get; }

    CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters);
}
