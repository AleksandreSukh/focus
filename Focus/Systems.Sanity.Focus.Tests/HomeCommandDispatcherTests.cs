#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Application.HomeCommands;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Tests;

public class HomeCommandDispatcherTests
{
    [Fact]
    public void DefaultDispatcher_HandlesEveryDefaultCatalogCommand()
    {
        using var workspace = new TestWorkspace();
        var context = new HomeCommandContext(workspace.AppContext);
        var dispatcher = HomeCommandHandlerRegistry.CreateDefault();
        var catalog = HomeCommandCatalog.CreateDefault(context, dispatcher);

        var commandIds = catalog.Descriptors
            .Select(descriptor => descriptor.CommandId)
            .Distinct()
            .ToArray();

        Assert.All(commandIds, commandId => Assert.True(dispatcher.CanHandle(commandId), commandId.ToString()));
    }

    [Fact]
    public void Constructor_RejectsDuplicateCommandHandlers()
    {
        var handlers = new IHomeCommandFeatureHandler[]
        {
            new StubHomeCommandFeatureHandler(HomeCommandId.CreateFile),
            new StubHomeCommandFeatureHandler(HomeCommandId.CreateFile)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new HomeCommandDispatcher(handlers));

        Assert.Contains("Duplicate home command handler for \"CreateFile\"", exception.Message);
    }

    [Fact]
    public void Execute_ReturnsUnsupportedCommandErrorWhenNoHandlerIsRegistered()
    {
        using var workspace = new TestWorkspace();
        var context = new HomeCommandContext(workspace.AppContext);
        var dispatcher = new HomeCommandDispatcher(Array.Empty<IHomeCommandFeatureHandler>());

        var result = dispatcher.Execute(
            context,
            (HomeCommandId)(-1),
            new ConsoleInput("unknown"),
            new Dictionary<int, FileInfo>());

        Assert.True(result.IsError);
        Assert.Equal("Unsupported home command \"-1\".", result.Message);
    }

    private sealed class StubHomeCommandFeatureHandler : IHomeCommandFeatureHandler
    {
        public StubHomeCommandFeatureHandler(params HomeCommandId[] commandIds)
        {
            CommandIds = commandIds;
        }

        public IReadOnlyCollection<HomeCommandId> CommandIds { get; }

        public HomeWorkflowResult Execute(
            HomeCommandContext context,
            HomeCommandId commandId,
            ConsoleInput input,
            IReadOnlyDictionary<int, FileInfo> fileSelection) =>
            HomeWorkflowResult.Continue;
    }
}
