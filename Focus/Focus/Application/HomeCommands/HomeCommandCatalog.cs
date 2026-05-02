#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Application.HomeCommands;

internal sealed class HomeCommandCatalog
{
    private const int SampleFileNumber = 1;
    private static readonly HomeCommandId[] CommandOptionsWithFiles =
    {
        HomeCommandId.CreateFile,
        HomeCommandId.RenameFile,
        HomeCommandId.DeleteFile,
        HomeCommandId.Refresh,
        HomeCommandId.Search,
        HomeCommandId.ListTasks,
        HomeCommandId.Exit,
        HomeCommandId.UpdateApp
    };

    private static readonly HomeCommandId[] CommandOptionsWithoutFiles =
    {
        HomeCommandId.CreateFile,
        HomeCommandId.Refresh,
        HomeCommandId.Search,
        HomeCommandId.ListTasks,
        HomeCommandId.Exit,
        HomeCommandId.UpdateApp
    };

    private readonly HomeCommandContext _context;
    private readonly IHomeCommandHandler _handler;
    private readonly IReadOnlyList<HomeCommandDescriptor> _descriptors;
    private readonly Dictionary<string, HomeCommandDescriptor> _descriptorsByKey;

    public HomeCommandCatalog(
        HomeCommandContext context,
        IHomeCommandHandler handler,
        IEnumerable<HomeCommandDescriptor> descriptors)
    {
        _context = context;
        _handler = handler;
        _descriptors = descriptors.ToArray();
        _descriptorsByKey = BuildDescriptorIndex(_descriptors);
    }

    public IReadOnlyList<HomeCommandDescriptor> Descriptors => _descriptors;

    public static HomeCommandCatalog CreateDefault(HomeCommandContext context, IHomeCommandHandler handler) =>
        new(context, handler, CreateDefaultDescriptors());

    public bool TryExecute(
        string commandKey,
        ConsoleInput input,
        IReadOnlyDictionary<int, FileInfo> fileSelection,
        out HomeWorkflowResult result)
    {
        result = HomeWorkflowResult.Continue;
        if (!_descriptorsByKey.TryGetValue(commandKey, out var descriptor))
            return false;

        result = _handler.Execute(_context, descriptor.CommandId, input, fileSelection);
        return true;
    }

    public bool TryGet(string commandKey, out HomeCommandDescriptor descriptor) =>
        _descriptorsByKey.TryGetValue(commandKey, out descriptor!);

    public IReadOnlyList<CommandHelpGroup> BuildHelpGroups(bool filesExist, string? updatedVersion)
    {
        var groups = BuildStaticHelpGroups(filesExist, updatedVersion);
        if (filesExist)
        {
            groups.Insert(1, new CommandHelpGroup("Open", new[]
            {
                SampleFileNumber.ToString(),
                AccessibleKeyNumbering.GetStringFor(SampleFileNumber)
            }));
        }

        return groups;
    }

    public IEnumerable<string> BuildCommandOptions(IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        var filesExist = fileSelection.Any();
        var commandOptions = BuildCommandOptionKeys(filesExist);

        if (!filesExist)
            return commandOptions;

        return fileSelection.Keys.Select(key => key.ToString())
            .Union(fileSelection.Keys.Select(AccessibleKeyNumbering.GetStringFor))
            .Union(fileSelection.Values.Select(file => file.NameWithoutExtension()))
            .Union(commandOptions);
    }

    public IEnumerable<string> BuildSuggestions(IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        var suggestions = BuildCommandOptions(fileSelection);
        if (!fileSelection.Any())
            return suggestions.Union(BuildTaskFilterSuggestions());

        return suggestions
            .Union(BuildParameterSuggestions(HomeCommandParameterSuggestionKind.SearchFileName, fileSelection))
            .Union(BuildTaskFilterSuggestions())
            .Union(BuildParameterSuggestions(HomeCommandParameterSuggestionKind.FileNumberAndShortcut, fileSelection));
    }

    private IEnumerable<string> BuildCommandOptionKeys(bool filesExist)
    {
        var commandIds = filesExist
            ? CommandOptionsWithFiles
            : CommandOptionsWithoutFiles;

        return commandIds
            .Select(GetDescriptorById)
            .SelectMany(descriptor => descriptor.Keys);
    }

    private List<CommandHelpGroup> BuildStaticHelpGroups(bool filesExist, string? updatedVersion)
    {
        var groupLabels = new List<string>();
        var entriesByGroup = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var descriptor in _descriptors)
        {
            if (descriptor.RequiresFiles && !filesExist)
                continue;
            if (descriptor.RequiresUpdateVersionInHelp && updatedVersion == null)
                continue;

            if (!entriesByGroup.TryGetValue(descriptor.HelpGroup, out var entries))
            {
                entries = new List<string>();
                entriesByGroup[descriptor.HelpGroup] = entries;
                groupLabels.Add(descriptor.HelpGroup);
            }

            entries.Add(BuildHelpEntry(descriptor, updatedVersion));
        }

        return groupLabels
            .Select(label => new CommandHelpGroup(label, entriesByGroup[label]))
            .ToList();
    }

    private static string BuildHelpEntry(HomeCommandDescriptor descriptor, string? updatedVersion) =>
        descriptor.RequiresUpdateVersionInHelp
            ? $"{descriptor.HelpEntry} ({updatedVersion})"
            : descriptor.HelpEntry;

    private IEnumerable<string> BuildTaskFilterSuggestions() =>
        BuildParameterSuggestions(HomeCommandParameterSuggestionKind.TaskFilter, new Dictionary<int, FileInfo>());

    private IEnumerable<string> BuildParameterSuggestions(
        HomeCommandParameterSuggestionKind suggestionKind,
        IReadOnlyDictionary<int, FileInfo> fileSelection) =>
        _descriptors
            .Where(descriptor => descriptor.ParameterSuggestionKind == suggestionKind)
            .SelectMany(descriptor => BuildParameterSuggestions(descriptor, fileSelection));

    private HomeCommandDescriptor GetDescriptorById(HomeCommandId commandId) =>
        _descriptors.Single(descriptor => descriptor.CommandId == commandId);

    private static IEnumerable<string> BuildParameterSuggestions(
        HomeCommandDescriptor descriptor,
        IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        switch (descriptor.ParameterSuggestionKind)
        {
            case HomeCommandParameterSuggestionKind.FileNumberAndShortcut:
                if (!fileSelection.Any())
                    return Array.Empty<string>();

                return descriptor.Keys
                    .SelectMany(key => fileSelection.Keys.Select(fileKey => $"{key} {fileKey}"))
                    .Concat(descriptor.Keys.SelectMany(key =>
                        fileSelection.Keys.Select(fileKey => $"{key} {AccessibleKeyNumbering.GetStringFor(fileKey)}")));

            case HomeCommandParameterSuggestionKind.SearchFileName:
                return !fileSelection.Any()
                    ? Array.Empty<string>()
                    : descriptor.Keys.SelectMany(key =>
                        fileSelection.Values.Select(file => $"{key} {file.NameWithoutExtension()}"));

            case HomeCommandParameterSuggestionKind.TaskFilter:
                return TaskCommandHelper.GetTaskListSuggestions(descriptor.Keys.ToArray());

            default:
                return Array.Empty<string>();
        }
    }

    private static Dictionary<string, HomeCommandDescriptor> BuildDescriptorIndex(
        IEnumerable<HomeCommandDescriptor> descriptors)
    {
        var descriptorsByKey = new Dictionary<string, HomeCommandDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in descriptors)
        {
            foreach (var key in descriptor.Keys)
            {
                if (descriptorsByKey.ContainsKey(key))
                    throw new InvalidOperationException($"Duplicate home command key \"{key}\".");

                descriptorsByKey[key] = descriptor;
            }
        }

        return descriptorsByKey;
    }

    private static IReadOnlyList<HomeCommandDescriptor> CreateDefaultDescriptors() =>
        new[]
        {
            new HomeCommandDescriptor(
                HomeCommandId.CreateFile,
                HomeCommandKeys.New,
                "Create",
                $"{HomeCommandKeys.New} <file name>"),

            new HomeCommandDescriptor(
                HomeCommandId.RenameFile,
                HomeCommandKeys.Rename,
                "Manage",
                $"{HomeCommandKeys.Rename} <file>",
                HomeCommandParameterSuggestionKind.FileNumberAndShortcut,
                requiresFiles: true),
            new HomeCommandDescriptor(
                HomeCommandId.DeleteFile,
                HomeCommandKeys.Delete,
                "Manage",
                $"{HomeCommandKeys.Delete} <file>",
                HomeCommandParameterSuggestionKind.FileNumberAndShortcut,
                requiresFiles: true),

            new HomeCommandDescriptor(
                HomeCommandId.Search,
                HomeCommandKeys.Search,
                "Find",
                $"{HomeCommandKeys.Search} <query>",
                HomeCommandParameterSuggestionKind.SearchFileName),
            new HomeCommandDescriptor(
                HomeCommandId.ListTasks,
                HomeCommandKeys.Tasks,
                "Find",
                $"{HomeCommandKeys.Tasks}/{HomeCommandKeys.TasksAlias} [todo|doing|done|all]",
                HomeCommandParameterSuggestionKind.TaskFilter,
                aliases: new[] { HomeCommandKeys.TasksAlias }),
            new HomeCommandDescriptor(
                HomeCommandId.Refresh,
                HomeCommandKeys.Refresh,
                "Find",
                HomeCommandKeys.Refresh),

            new HomeCommandDescriptor(
                HomeCommandId.UpdateApp,
                HomeCommandKeys.UpdateApp,
                "System",
                HomeCommandKeys.UpdateApp,
                requiresUpdateVersionInHelp: true),
            new HomeCommandDescriptor(
                HomeCommandId.Exit,
                HomeCommandKeys.Exit,
                "System",
                HomeCommandKeys.Exit)
        };
}
