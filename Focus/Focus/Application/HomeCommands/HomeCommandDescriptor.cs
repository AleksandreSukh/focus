#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Systems.Sanity.Focus.Application.HomeCommands;

internal sealed class HomeCommandDescriptor
{
    public HomeCommandDescriptor(
        HomeCommandId commandId,
        string primaryKey,
        string helpGroup,
        string helpEntry,
        HomeCommandParameterSuggestionKind parameterSuggestionKind = HomeCommandParameterSuggestionKind.None,
        bool requiresFiles = false,
        bool requiresUpdateVersionInHelp = false,
        params string[] aliases)
    {
        if (string.IsNullOrWhiteSpace(primaryKey))
            throw new ArgumentException("Command key is required.", nameof(primaryKey));
        if (string.IsNullOrWhiteSpace(helpGroup))
            throw new ArgumentException("Help group is required.", nameof(helpGroup));
        if (string.IsNullOrWhiteSpace(helpEntry))
            throw new ArgumentException("Help entry is required.", nameof(helpEntry));

        CommandId = commandId;
        PrimaryKey = primaryKey;
        HelpGroup = helpGroup;
        HelpEntry = helpEntry;
        ParameterSuggestionKind = parameterSuggestionKind;
        RequiresFiles = requiresFiles;
        RequiresUpdateVersionInHelp = requiresUpdateVersionInHelp;
        Aliases = aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .ToArray();
        Keys = new[] { PrimaryKey }
            .Concat(Aliases)
            .ToArray();
    }

    public HomeCommandId CommandId { get; }

    public string PrimaryKey { get; }

    public IReadOnlyList<string> Aliases { get; }

    public IReadOnlyList<string> Keys { get; }

    public string HelpGroup { get; }

    public string HelpEntry { get; }

    public HomeCommandParameterSuggestionKind ParameterSuggestionKind { get; }

    public bool RequiresFiles { get; }

    public bool RequiresUpdateVersionInHelp { get; }
}
