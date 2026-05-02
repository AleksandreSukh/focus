#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.Application.EditCommands;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Application;

internal sealed class EditWorkflow
{
    private readonly FocusAppContext _appContext;
    private readonly EditCommandContext _commandContext;
    private readonly EditCommandCatalog _commandCatalog;
    private readonly EditFallbackCommandProcessor _fallbackCommandProcessor = new();
    private string _filePath;
    private MindMap _map;

    public EditWorkflow(string filePath, FocusAppContext appContext, Guid? initialNodeIdentifier = null)
    {
        _filePath = filePath;
        _appContext = appContext;
        _map = _appContext.MapRepository.OpenMapForEditing(filePath);
        _commandContext = new EditCommandContext(
            appContext,
            () => _map,
            map => _map = map,
            () => _filePath,
            newFilePath => _filePath = newFilePath);
        _commandCatalog = EditCommandCatalog.CreateDefault(
            _commandContext,
            EditCommandHandlerRegistry.CreateDefault());
        _appContext.RefreshLinkIndex();
        if (initialNodeIdentifier.HasValue)
        {
            _map.ChangeCurrentNodeById(initialNodeIdentifier.Value);
        }
    }

    public string FilePath => _filePath;

    public string FileTitle => Path.GetFileName(_filePath) ?? string.Empty;

    public string BuildScreen(string? message = null, bool isError = false, bool showCommands = true)
    {
        var screenBuilder = new StringBuilder(BuildCurrentSubtreeString());

        if (!string.IsNullOrEmpty(message))
        {
            var messagePrefix = isError ? ":!" : ":i";
            screenBuilder.Append($"{messagePrefix} {message}{Environment.NewLine}{Environment.NewLine}");
        }

        if (_appContext.LinkIndex.HasQueuedLinkSources)
        {
            screenBuilder.Append(
                $":Nodes to be linked> {string.Join("; ", _appContext.LinkIndex.QueuedLinkSources.Select(node => NodeDisplayHelper.GetSingleLinePreview(node.Name)))}{Environment.NewLine}");
        }

        screenBuilder.Append(EditAttachmentOperations.BuildCurrentAttachmentSummary(_commandContext));

        if (showCommands)
        {
            screenBuilder.Append(BuildCommandHelpText());
        }
        else
        {
            screenBuilder.Append(CommandHelpText.BuildHiddenHelpLine());
        }

        return screenBuilder.ToString();
    }

    public CommandExecutionResult Execute(ConsoleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.InputString))
            return CommandExecutionResult.Error("Empty command");

        var command = input.FirstWord.ToCommandKey();
        var parameters = input.Parameters;

        try
        {
            return command switch
            {
                _ when _commandCatalog.TryExecute(command, parameters, out var commandResult) => commandResult,
                _ => _fallbackCommandProcessor.Execute(_commandContext, command, input)
            };
        }
        catch (Exception ex)
        {
            return CommandExecutionResult.Error(ExceptionDiagnostics.LogException(ex, "executing map command"));
        }
    }

    public IEnumerable<string> GetSuggestions()
    {
        var childNodes = _map.GetChildren();
        return GetCommandOptions()
            .Union(_commandCatalog.BuildParameterSuggestions(
                childNodes,
                EditAttachmentOperations.GetCurrentAttachmentSelectors(_commandContext).ToArray()));
    }

    public void Save(string commitMessage)
    {
        if (string.IsNullOrWhiteSpace(commitMessage))
            throw new ArgumentException("Sync commit message is required.", nameof(commitMessage));

        _appContext.MapRepository.SaveMap(_filePath, _map);
        _appContext.RefreshLinkIndex();
        _appContext.MapsStorage.Sync(commitMessage);
    }

    private string BuildCommandHelpText()
    {
        var childNodes = _map.GetChildren()
            .OrderBy(node => node.Key)
            .ToArray();
        var helpGroups = new List<CommandHelpGroup>();
        if (childNodes.Any())
        {
            helpGroups.Add(new CommandHelpGroup(
                "Go to",
                BuildGoToEntries(childNodes)));
        }

        helpGroups.AddRange(_commandCatalog.BuildHelpGroups());

        return CommandHelpFormatter.BuildGroupedLines(helpGroups);
    }

    private static IReadOnlyList<string> BuildGoToEntries(IEnumerable<KeyValuePair<int, string>> childNodes)
    {
        var orderedNodes = childNodes
            .OrderBy(node => node.Key)
            .ToArray();

        return new[]
        {
            BuildGoToEntry("text", orderedNodes.Select(node => AccessibleKeyNumbering.GetStringFor(node.Key))),
            BuildGoToEntry("numbers", orderedNodes.Select(node => node.Key.ToString()))
        };
    }

    private static string BuildGoToEntry(string label, IEnumerable<string?> items)
    {
        var itemList = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
        var visibleItems = itemList.Take(5).ToList();

        if (itemList.Count > 5)
            visibleItems.Add("...");

        return $"{label}: {string.Join(", ", visibleItems)}";
    }

    private string BuildCurrentSubtreeString()
    {
        var sb = new StringBuilder();
        NodePrinter.Print(
            _map.GetCurrentNode(),
            _appContext.LinkIndex,
            ConfigurationConstants.NodePrinting.LeftBorder,
            false,
            0,
            sb,
            AppConsole.Current.WindowWidth - 5,
            NodeBranchVisibility.HideDoneStateForNode(_map.GetCurrentNode()));
        return sb.ToString();
    }

    private string[] GetCommandOptions()
    {
        var childNodes = _map.GetChildren();
        var childSelectors = childNodes.Keys
            .SelectMany(key => new[] { key.ToString(), AccessibleKeyNumbering.GetStringFor(key) })
            .Where(selector => !string.IsNullOrWhiteSpace(selector))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var currentAttachmentSelectors = EditAttachmentOperations.GetCurrentAttachmentSelectors(_commandContext).ToArray();
        var bareAttachmentSelectors = currentAttachmentSelectors
            .Where(selector => !childSelectors.Contains(selector));

        return _commandCatalog.GetCommandKeys()
            .Union(childNodes.Keys.Select(key => key.ToString()))
            .Union(childNodes.Keys.Select(AccessibleKeyNumbering.GetStringFor))
            .Union(bareAttachmentSelectors)
            .Union(_commandCatalog.BuildAttachmentCommandSuggestions(currentAttachmentSelectors))
            .ToArray();
    }
}
