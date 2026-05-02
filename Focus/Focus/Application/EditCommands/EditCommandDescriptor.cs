#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditCommandDescriptor
{
    public EditCommandDescriptor(
        EditCommandId commandId,
        string primaryKey,
        string helpGroup,
        string helpEntry,
        EditCommandParameterSuggestionKind parameterSuggestionKind = EditCommandParameterSuggestionKind.None,
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
        Aliases = aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .ToArray();
        Keys = new[] { PrimaryKey }
            .Concat(Aliases)
            .ToArray();
    }

    public EditCommandId CommandId { get; }

    public string PrimaryKey { get; }

    public IReadOnlyList<string> Aliases { get; }

    public IReadOnlyList<string> Keys { get; }

    public string HelpGroup { get; }

    public string HelpEntry { get; }

    public EditCommandParameterSuggestionKind ParameterSuggestionKind { get; }
}
