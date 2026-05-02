#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditCommandCatalog
{
    private readonly EditCommandContext _context;
    private readonly IEditCommandHandler _handler;
    private readonly IReadOnlyList<EditCommandDescriptor> _descriptors;
    private readonly Dictionary<string, EditCommandDescriptor> _descriptorsByKey;

    public EditCommandCatalog(
        EditCommandContext context,
        IEditCommandHandler handler,
        IEnumerable<EditCommandDescriptor> descriptors)
    {
        _context = context;
        _handler = handler;
        _descriptors = descriptors.ToArray();
        _descriptorsByKey = BuildDescriptorIndex(_descriptors);
    }

    public IReadOnlyList<EditCommandDescriptor> Descriptors => _descriptors;

    public static EditCommandCatalog CreateDefault(EditCommandContext context, IEditCommandHandler handler) =>
        new(context, handler, CreateDefaultDescriptors());

    public bool TryExecute(string commandKey, string parameters, out CommandExecutionResult result)
    {
        result = CommandExecutionResult.Success();
        if (!_descriptorsByKey.TryGetValue(commandKey, out var descriptor))
            return false;

        result = _handler.Execute(_context, descriptor.CommandId, parameters);
        return true;
    }

    public bool TryGet(string commandKey, out EditCommandDescriptor descriptor) =>
        _descriptorsByKey.TryGetValue(commandKey, out descriptor!);

    public IReadOnlyList<CommandHelpGroup> BuildHelpGroups()
    {
        var groupLabels = new List<string>();
        var entriesByGroup = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var descriptor in _descriptors)
        {
            if (!entriesByGroup.TryGetValue(descriptor.HelpGroup, out var entries))
            {
                entries = new List<string>();
                entriesByGroup[descriptor.HelpGroup] = entries;
                groupLabels.Add(descriptor.HelpGroup);
            }

            entries.Add(descriptor.HelpEntry);
        }

        return groupLabels
            .Select(label => new CommandHelpGroup(label, entriesByGroup[label]))
            .ToArray();
    }

    public IEnumerable<string> GetCommandKeys() =>
        _descriptors.SelectMany(descriptor => descriptor.Keys);

    public IEnumerable<string> BuildParameterSuggestions(
        IReadOnlyDictionary<int, string> childNodes,
        IReadOnlyCollection<string> attachmentSelectors)
    {
        foreach (var descriptor in _descriptors)
        {
            foreach (var suggestion in BuildParameterSuggestions(descriptor, childNodes, attachmentSelectors))
                yield return suggestion;
        }
    }

    public IEnumerable<string> BuildAttachmentCommandSuggestions(IReadOnlyCollection<string> attachmentSelectors)
    {
        if (attachmentSelectors.Count == 0)
            return Array.Empty<string>();

        return _descriptors
            .Where(descriptor => descriptor.ParameterSuggestionKind == EditCommandParameterSuggestionKind.AttachmentSelector)
            .SelectMany(descriptor => descriptor.Keys.SelectMany(key => attachmentSelectors.Select(selector => $"{key} {selector}")));
    }

    private static IEnumerable<string> BuildParameterSuggestions(
        EditCommandDescriptor descriptor,
        IReadOnlyDictionary<int, string> childNodes,
        IReadOnlyCollection<string> attachmentSelectors)
    {
        switch (descriptor.ParameterSuggestionKind)
        {
            case EditCommandParameterSuggestionKind.ChildNumberAndPreview:
                if (childNodes.Count == 0)
                    return Array.Empty<string>();

                return descriptor.Keys
                    .SelectMany(key => childNodes.Keys.Select(childKey => $"{key} {childKey}"))
                    .Concat(descriptor.Keys.SelectMany(key => childNodes.Values.Select(value => $"{key} {value}")));

            case EditCommandParameterSuggestionKind.SearchChildPreview:
                return childNodes.Count == 0
                    ? Array.Empty<string>()
                    : descriptor.Keys.SelectMany(key => childNodes.Values.Select(value => $"{key} {value}"));

            case EditCommandParameterSuggestionKind.TaskFilter:
                return TaskCommandHelper.GetTaskListSuggestions(descriptor.Keys.ToArray());

            case EditCommandParameterSuggestionKind.AttachmentSelector:
                return attachmentSelectors.Count == 0
                    ? Array.Empty<string>()
                    : descriptor.Keys.SelectMany(key => attachmentSelectors.Select(selector => $"{key} {selector}"));

            default:
                return Array.Empty<string>();
        }
    }

    private static Dictionary<string, EditCommandDescriptor> BuildDescriptorIndex(
        IEnumerable<EditCommandDescriptor> descriptors)
    {
        var descriptorsByKey = new Dictionary<string, EditCommandDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in descriptors)
        {
            foreach (var key in descriptor.Keys)
            {
                if (descriptorsByKey.ContainsKey(key))
                    throw new InvalidOperationException($"Duplicate edit command key \"{key}\".");

                descriptorsByKey[key] = descriptor;
            }
        }

        return descriptorsByKey;
    }

    private static IReadOnlyList<EditCommandDescriptor> CreateDefaultDescriptors() =>
        new[]
        {
            new EditCommandDescriptor(
                EditCommandId.GoTo,
                "cd",
                "Navigate",
                "cd <child>",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(EditCommandId.GoUp, "up", "Navigate", "up"),
            new EditCommandDescriptor(EditCommandId.GoToRoot, "ls", "Navigate", "ls"),

            new EditCommandDescriptor(EditCommandId.Add, "add", "Edit", "add"),
            new EditCommandDescriptor(EditCommandId.AddBlock, "addblock", "Edit", "addblock"),
            new EditCommandDescriptor(EditCommandId.AddIdea, "idea", "Edit", "idea"),
            new EditCommandDescriptor(
                EditCommandId.Edit,
                "edit",
                "Edit",
                "edit [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(
                EditCommandId.Delete,
                "del",
                "Edit",
                "del [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(EditCommandId.ClearIdeas, "clearideas", "Edit", "clearideas [child]"),
            new EditCommandDescriptor(
                EditCommandId.Slice,
                "slice",
                "Edit",
                "slice [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(
                EditCommandId.Hide,
                "min",
                "Edit",
                "min <child>",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(
                EditCommandId.Unhide,
                "max",
                "Edit",
                "max <child>",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(EditCommandId.Capture, "capture", "Edit", "capture"),
            new EditCommandDescriptor(EditCommandId.Voice, "voice", "Edit", "voice"),

            new EditCommandDescriptor(
                EditCommandId.SetTaskTodo,
                "todo",
                "To Do",
                "todo/td [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview,
                "td"),
            new EditCommandDescriptor(
                EditCommandId.SetTaskDoing,
                "doing",
                "To Do",
                "doing [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(
                EditCommandId.SetTaskDone,
                "done",
                "To Do",
                "done/dn [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview,
                "dn"),
            new EditCommandDescriptor(
                EditCommandId.ClearTaskState,
                "notask",
                "To Do",
                "notask [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(
                EditCommandId.ToggleTaskState,
                "toggle",
                "To Do",
                "toggle/tg [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview,
                "tg"),
            new EditCommandDescriptor(
                EditCommandId.HideDoneTasks,
                "hidedone",
                "To Do",
                "hidedone [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(
                EditCommandId.ShowDoneTasks,
                "showdone",
                "To Do",
                "showdone [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(
                EditCommandId.ListTasks,
                "tasks",
                "To Do",
                "tasks/ts [todo|doing|done|all]",
                EditCommandParameterSuggestionKind.TaskFilter,
                "ts"),

            new EditCommandDescriptor(
                EditCommandId.LinkFrom,
                "linkfrom",
                "Links",
                "linkfrom <child>",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(
                EditCommandId.LinkTo,
                "linkto",
                "Links",
                "linkto <child>",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(EditCommandId.OpenLink, "openlink", "Links", "openlink"),
            new EditCommandDescriptor(EditCommandId.Backlinks, "backlinks", "Links", "backlinks"),

            new EditCommandDescriptor(
                EditCommandId.Search,
                "search",
                "Search/Export",
                "search <query>",
                EditCommandParameterSuggestionKind.SearchChildPreview),
            new EditCommandDescriptor(EditCommandId.Export, "export", "Search/Export", "export"),
            new EditCommandDescriptor(
                EditCommandId.Meta,
                "meta",
                "Search/Export",
                "meta [child]",
                EditCommandParameterSuggestionKind.ChildNumberAndPreview),
            new EditCommandDescriptor(
                EditCommandId.Attachments,
                "attachments",
                "Search/Export",
                "attachments [attachment]",
                EditCommandParameterSuggestionKind.AttachmentSelector),

            new EditCommandDescriptor(EditCommandId.Exit, "exit", "System", "exit")
        };
}
