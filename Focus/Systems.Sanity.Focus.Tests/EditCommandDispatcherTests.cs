#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Application.EditCommands;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Tests;

public class EditCommandDispatcherTests
{
    [Fact]
    public void DefaultDispatcher_HandlesEveryDefaultCatalogCommand()
    {
        using var workspace = new TestWorkspace();
        var context = CreateContext(workspace);
        var dispatcher = EditCommandHandlerRegistry.CreateDefault();
        var catalog = EditCommandCatalog.CreateDefault(context, dispatcher);

        var commandIds = catalog.Descriptors
            .Select(descriptor => descriptor.CommandId)
            .Distinct()
            .ToArray();

        Assert.All(commandIds, commandId => Assert.True(dispatcher.CanHandle(commandId), commandId.ToString()));
    }

    [Fact]
    public void Constructor_RejectsDuplicateCommandHandlers()
    {
        var handlers = new IEditCommandFeatureHandler[]
        {
            new StubEditCommandFeatureHandler(EditCommandId.Add),
            new StubEditCommandFeatureHandler(EditCommandId.Add)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new EditCommandDispatcher(handlers));

        Assert.Contains("Duplicate edit command handler for \"Add\"", exception.Message);
    }

    [Fact]
    public void Execute_ReturnsUnsupportedCommandErrorWhenNoHandlerIsRegistered()
    {
        using var workspace = new TestWorkspace();
        var context = CreateContext(workspace);
        var dispatcher = new EditCommandDispatcher(Array.Empty<IEditCommandFeatureHandler>());

        var result = dispatcher.Execute(context, (EditCommandId)(-1), string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unsupported command \"-1\"", result.ErrorString);
    }

    private static EditCommandContext CreateContext(TestWorkspace workspace)
    {
        var map = new MindMap("Root");
        var filePath = workspace.SaveMap("dispatcher-map", map);
        return new EditCommandContext(
            workspace.AppContext,
            () => map,
            newMap => map = newMap,
            () => filePath,
            newFilePath => filePath = newFilePath);
    }

    private sealed class StubEditCommandFeatureHandler : IEditCommandFeatureHandler
    {
        public StubEditCommandFeatureHandler(params EditCommandId[] commandIds)
        {
            CommandIds = commandIds;
        }

        public IReadOnlyCollection<EditCommandId> CommandIds { get; }

        public CommandExecutionResult Execute(
            EditCommandContext context,
            EditCommandId commandId,
            string parameters) =>
            CommandExecutionResult.Success();
    }
}
