#nullable enable

using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.HomeCommands;

internal interface IHomeCommandFeatureHandler
{
    IReadOnlyCollection<HomeCommandId> CommandIds { get; }

    HomeWorkflowResult Execute(
        HomeCommandContext context,
        HomeCommandId commandId,
        ConsoleInput input,
        IReadOnlyDictionary<int, FileInfo> fileSelection);
}
