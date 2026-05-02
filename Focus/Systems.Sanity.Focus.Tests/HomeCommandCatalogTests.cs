#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Application.HomeCommands;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Tests;

public class HomeCommandCatalogTests
{
    [Fact]
    public void DefaultCatalog_ResolvesPrimaryKeysAndAliases()
    {
        using var workspace = new TestWorkspace();
        var catalog = CreateCatalog(workspace, out var handler);

        var expectedCommands = new Dictionary<string, HomeCommandId>
        {
            [HomeCommandKeys.New] = HomeCommandId.CreateFile,
            [HomeCommandKeys.Rename] = HomeCommandId.RenameFile,
            [HomeCommandKeys.Delete] = HomeCommandId.DeleteFile,
            [HomeCommandKeys.Exit] = HomeCommandId.Exit,
            [HomeCommandKeys.Refresh] = HomeCommandId.Refresh,
            [HomeCommandKeys.UpdateApp] = HomeCommandId.UpdateApp,
            [HomeCommandKeys.Search] = HomeCommandId.Search,
            [HomeCommandKeys.Tasks] = HomeCommandId.ListTasks,
            [HomeCommandKeys.TasksAlias] = HomeCommandId.ListTasks
        };

        foreach (var expectedCommand in expectedCommands)
        {
            Assert.True(catalog.TryGet(expectedCommand.Key, out var descriptor));
            Assert.Equal(expectedCommand.Value, descriptor.CommandId);
        }

        Assert.True(catalog.TryGet(HomeCommandKeys.Tasks, out var tasksDescriptor));
        Assert.True(catalog.TryGet(HomeCommandKeys.TasksAlias, out var tasksAliasDescriptor));
        Assert.Same(tasksDescriptor, tasksAliasDescriptor);

        Assert.True(catalog.TryExecute(
            "TS",
            new ConsoleInput("TS"),
            CreateEmptySelection(),
            out var result));
        Assert.False(result.IsError);
        Assert.Equal(HomeCommandId.ListTasks, handler.ExecutedCommandIds.Single());
    }

    [Fact]
    public void Constructor_RejectsDuplicateCommandKeys()
    {
        using var workspace = new TestWorkspace();
        var context = new HomeCommandContext(workspace.AppContext);
        var handler = new RecordingHomeCommandHandler();
        var descriptors = new[]
        {
            new HomeCommandDescriptor(HomeCommandId.CreateFile, "new", "Create", "new"),
            new HomeCommandDescriptor(HomeCommandId.Refresh, "ls", "Find", "ls", aliases: new[] { "new" })
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => new HomeCommandCatalog(context, handler, descriptors));

        Assert.Contains("Duplicate home command key \"new\"", exception.Message);
    }

    [Fact]
    public void BuildHelpGroups_WithoutFiles_PreservesCurrentOrderAndHidesUpdate()
    {
        using var workspace = new TestWorkspace();
        var catalog = CreateCatalog(workspace, out _);

        var groups = catalog.BuildHelpGroups(filesExist: false, updatedVersion: null);

        Assert.Equal(new[] { "Create", "Find", "System" }, groups.Select(group => group.Label));
        Assert.Equal(new[] { "new <file name>" }, groups.Single(group => group.Label == "Create").Entries);
        Assert.Equal(
            new[] { "search <query>", "tasks/ts [todo|doing|done|all]", "ls" },
            groups.Single(group => group.Label == "Find").Entries);
        Assert.Equal(new[] { "exit" }, groups.Single(group => group.Label == "System").Entries);
        Assert.DoesNotContain(groups, group => group.Label == "Open");
        Assert.DoesNotContain(groups, group => group.Label == "Manage");
    }

    [Fact]
    public void BuildHelpGroups_WithFiles_PreservesOpenManageOrderAndShowsUpdateVersion()
    {
        using var workspace = new TestWorkspace();
        var catalog = CreateCatalog(workspace, out _);

        var groups = catalog.BuildHelpGroups(filesExist: true, updatedVersion: "2.0.0");

        Assert.Equal(new[] { "Create", "Open", "Manage", "Find", "System" }, groups.Select(group => group.Label));
        Assert.Equal(
            new[] { "1", AccessibleKeyNumbering.GetStringFor(1) },
            groups.Single(group => group.Label == "Open").Entries);
        Assert.Equal(new[] { "ren <file>", "del <file>" }, groups.Single(group => group.Label == "Manage").Entries);
        Assert.Equal(new[] { "update (2.0.0)", "exit" }, groups.Single(group => group.Label == "System").Entries);
    }

    [Fact]
    public void BuildCommandOptions_PreservesFileSelectorsAndCommandAvailability()
    {
        using var workspace = new TestWorkspace();
        var catalog = CreateCatalog(workspace, out _);
        var fileSelection = CreateFileSelection();

        var optionsWithFiles = catalog.BuildCommandOptions(fileSelection).ToArray();
        var optionsWithoutFiles = catalog.BuildCommandOptions(CreateEmptySelection()).ToArray();

        Assert.Equal(
            new[]
            {
                "1",
                AccessibleKeyNumbering.GetStringFor(1),
                "alpha",
                HomeCommandKeys.New,
                HomeCommandKeys.Rename,
                HomeCommandKeys.Delete,
                HomeCommandKeys.Refresh,
                HomeCommandKeys.Search,
                HomeCommandKeys.Tasks,
                HomeCommandKeys.TasksAlias,
                HomeCommandKeys.Exit,
                HomeCommandKeys.UpdateApp
            },
            optionsWithFiles);
        Assert.Equal(
            new[]
            {
                HomeCommandKeys.New,
                HomeCommandKeys.Refresh,
                HomeCommandKeys.Search,
                HomeCommandKeys.Tasks,
                HomeCommandKeys.TasksAlias,
                HomeCommandKeys.Exit,
                HomeCommandKeys.UpdateApp
            },
            optionsWithoutFiles);
        Assert.Contains("1", optionsWithFiles);
        Assert.Contains(AccessibleKeyNumbering.GetStringFor(1), optionsWithFiles);
        Assert.Contains("alpha", optionsWithFiles);
        Assert.Contains(HomeCommandKeys.Rename, optionsWithFiles);
        Assert.Contains(HomeCommandKeys.Delete, optionsWithFiles);
        Assert.Contains(HomeCommandKeys.UpdateApp, optionsWithFiles);

        Assert.Contains(HomeCommandKeys.New, optionsWithoutFiles);
        Assert.Contains(HomeCommandKeys.Refresh, optionsWithoutFiles);
        Assert.Contains(HomeCommandKeys.Search, optionsWithoutFiles);
        Assert.Contains(HomeCommandKeys.Tasks, optionsWithoutFiles);
        Assert.Contains(HomeCommandKeys.TasksAlias, optionsWithoutFiles);
        Assert.Contains(HomeCommandKeys.Exit, optionsWithoutFiles);
        Assert.Contains(HomeCommandKeys.UpdateApp, optionsWithoutFiles);
        Assert.DoesNotContain(HomeCommandKeys.Rename, optionsWithoutFiles);
        Assert.DoesNotContain(HomeCommandKeys.Delete, optionsWithoutFiles);
    }

    [Fact]
    public void BuildSuggestions_PreservesTaskSearchAndFileParameterSuggestions()
    {
        using var workspace = new TestWorkspace();
        var catalog = CreateCatalog(workspace, out _);
        var fileSelection = CreateFileSelection();

        var suggestionsWithFiles = catalog.BuildSuggestions(fileSelection).ToArray();
        var suggestionsWithoutFiles = catalog.BuildSuggestions(CreateEmptySelection()).ToArray();

        Assert.Contains("search alpha", suggestionsWithFiles);
        Assert.Contains("tasks done", suggestionsWithFiles);
        Assert.Contains("ts all", suggestionsWithFiles);
        Assert.Contains("ren 1", suggestionsWithFiles);
        Assert.Contains($"del {AccessibleKeyNumbering.GetStringFor(1)}", suggestionsWithFiles);
        Assert.DoesNotContain("ren alpha", suggestionsWithFiles);
        Assert.DoesNotContain("del alpha", suggestionsWithFiles);

        Assert.Contains(HomeCommandKeys.New, suggestionsWithoutFiles);
        Assert.Contains("tasks done", suggestionsWithoutFiles);
        Assert.Contains("ts all", suggestionsWithoutFiles);
        Assert.DoesNotContain(HomeCommandKeys.Rename, suggestionsWithoutFiles);
        Assert.DoesNotContain(HomeCommandKeys.Delete, suggestionsWithoutFiles);
        Assert.DoesNotContain("search alpha", suggestionsWithoutFiles);
    }

    private static HomeCommandCatalog CreateCatalog(
        TestWorkspace workspace,
        out RecordingHomeCommandHandler handler)
    {
        handler = new RecordingHomeCommandHandler();
        return HomeCommandCatalog.CreateDefault(new HomeCommandContext(workspace.AppContext), handler);
    }

    private static IReadOnlyDictionary<int, FileInfo> CreateEmptySelection() =>
        new Dictionary<int, FileInfo>();

    private static IReadOnlyDictionary<int, FileInfo> CreateFileSelection() =>
        new Dictionary<int, FileInfo>
        {
            [1] = new(Path.Combine(Path.GetTempPath(), "alpha.json"))
        };

    private sealed class RecordingHomeCommandHandler : IHomeCommandHandler
    {
        public List<HomeCommandId> ExecutedCommandIds { get; } = new();

        public HomeWorkflowResult Execute(
            HomeCommandContext context,
            HomeCommandId commandId,
            ConsoleInput input,
            IReadOnlyDictionary<int, FileInfo> fileSelection)
        {
            ExecutedCommandIds.Add(commandId);
            return HomeWorkflowResult.Continue;
        }
    }
}
