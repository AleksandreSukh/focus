#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Application.EditCommands;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Tests;

public class EditCommandCatalogTests
{
    [Fact]
    public void DefaultCatalog_ResolvesPrimaryKeysAndAliases()
    {
        using var workspace = new TestWorkspace();
        var catalog = CreateCatalog(workspace, out var handler);

        Assert.True(catalog.TryExecute("addblock", string.Empty, out var addBlockResult));
        Assert.True(addBlockResult.IsSuccess);
        Assert.Equal(EditCommandId.AddBlock, handler.ExecutedCommandIds.Single());

        Assert.True(catalog.TryGet("todo", out var todoDescriptor));
        Assert.True(catalog.TryGet("td", out var todoAliasDescriptor));
        Assert.Same(todoDescriptor, todoAliasDescriptor);
        Assert.Equal(EditCommandId.SetTaskTodo, todoDescriptor.CommandId);
    }

    [Fact]
    public void Constructor_RejectsDuplicateCommandKeys()
    {
        using var workspace = new TestWorkspace();
        var context = CreateContext(workspace);
        var handler = new RecordingEditCommandHandler();
        var descriptors = new[]
        {
            new EditCommandDescriptor(EditCommandId.Add, "add", "Edit", "add"),
            new EditCommandDescriptor(EditCommandId.AddBlock, "addblock", "Edit", "addblock", aliases: new[] { "add" })
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => new EditCommandCatalog(context, handler, descriptors));

        Assert.Contains("Duplicate edit command key \"add\"", exception.Message);
    }

    [Fact]
    public void BuildHelpGroups_PreservesRegisteredGroupOrder()
    {
        using var workspace = new TestWorkspace();
        var catalog = CreateCatalog(workspace, out _);

        var groups = catalog.BuildHelpGroups();

        Assert.Equal(
            new[] { "Navigate", "Edit", "To Do", "Links", "Search/Export", "System" },
            groups.Select(group => group.Label));
        Assert.Contains(groups.Single(group => group.Label == "Edit").Entries, entry => entry == "addblock");
        Assert.Contains(groups.Single(group => group.Label == "Edit").Entries, entry => entry == "star [child]");
        Assert.Contains(groups.Single(group => group.Label == "Edit").Entries, entry => entry == "unstar [child]");
        Assert.Contains(groups.Single(group => group.Label == "To Do").Entries, entry => entry == "todo/td [child]");
        Assert.Contains(groups.Single(group => group.Label == "Search/Export").Entries, entry => entry == "attachments [attachment]");
    }

    [Fact]
    public void BuildParameterSuggestions_UsesDescriptorSuggestionModes()
    {
        using var workspace = new TestWorkspace();
        var catalog = CreateCatalog(workspace, out _);
        var childNodes = new Dictionary<int, string> { [1] = "Child" };
        var attachmentSelectors = new[] { "1", "ja" };

        var suggestions = catalog.BuildParameterSuggestions(childNodes, attachmentSelectors).ToArray();

        Assert.Contains("cd 1", suggestions);
        Assert.Contains("edit Child", suggestions);
        Assert.Contains("td 1", suggestions);
        Assert.Contains("star 1", suggestions);
        Assert.Contains("unstar Child", suggestions);
        Assert.Contains("search Child", suggestions);
        Assert.Contains("tasks done", suggestions);
        Assert.Contains("ts all", suggestions);
        Assert.Contains("attachments 1", suggestions);
        Assert.DoesNotContain("clearideas 1", suggestions);
    }

    private static EditCommandCatalog CreateCatalog(
        TestWorkspace workspace,
        out RecordingEditCommandHandler handler)
    {
        handler = new RecordingEditCommandHandler();
        return EditCommandCatalog.CreateDefault(CreateContext(workspace), handler);
    }

    private static EditCommandContext CreateContext(TestWorkspace workspace)
    {
        var map = new MindMap("Root");
        var filePath = workspace.SaveMap("catalog-map", map);
        return new EditCommandContext(
            workspace.AppContext,
            () => map,
            newMap => map = newMap,
            () => filePath,
            newFilePath => filePath = newFilePath);
    }

    private sealed class RecordingEditCommandHandler : IEditCommandHandler
    {
        public List<EditCommandId> ExecutedCommandIds { get; } = new();

        public CommandExecutionResult Execute(
            EditCommandContext context,
            EditCommandId commandId,
            string parameters)
        {
            ExecutedCommandIds.Add(commandId);
            return CommandExecutionResult.Success();
        }
    }
}
