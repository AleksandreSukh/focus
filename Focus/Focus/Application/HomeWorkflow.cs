#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.Application.HomeCommands;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Application;

internal sealed class HomeWorkflow
{
    private readonly FocusAppContext _appContext;
    private readonly HomeCommandContext _commandContext;
    private readonly HomeCommandCatalog _commandCatalog;
    private readonly HomeFileOpenFallbackProcessor _fileOpenFallbackProcessor = new();

    public HomeWorkflow(FocusAppContext appContext)
    {
        _appContext = appContext;
        _commandContext = new HomeCommandContext(appContext);
        _commandCatalog = HomeCommandCatalog.CreateDefault(
            _commandContext,
            HomeCommandHandlerRegistry.CreateDefault());
    }

    public Dictionary<int, FileInfo> GetFileSelection()
    {
        return _appContext.MapSelectionService.GetTopSelection();
    }

    public string BuildHomePageText(IReadOnlyDictionary<int, FileInfo> files, bool showCommands)
    {
        var commandColor = ConfigurationConstants.CommandColor;
        var homePageMenuTextBuilder = new StringBuilder();

        foreach (var file in files)
        {
            homePageMenuTextBuilder.AppendLine(
                $"[{commandColor}]{AccessibleKeyNumbering.GetStringFor(file.Key)}[!]/[{commandColor}]{file.Key}[!] - {file.Value.NameWithoutExtension()}.");
        }

        homePageMenuTextBuilder.AppendLine();

        if (showCommands)
        {
            var updatedVersion = AutoUpdateManager.CheckUpdatedVersion();
            homePageMenuTextBuilder.Append(CommandHelpFormatter.BuildGroupedLines(
                _commandCatalog.BuildHelpGroups(files.Any(), updatedVersion)));
        }
        else
        {
            homePageMenuTextBuilder.Append(CommandHelpText.BuildHiddenHelpLine());
        }

        return homePageMenuTextBuilder.ToString();
    }

    public string[] GetCommandOptions(IReadOnlyDictionary<int, FileInfo> fileSelection) =>
        _commandCatalog.BuildCommandOptions(fileSelection).ToArray();

    public IEnumerable<string> GetSuggestions(IReadOnlyDictionary<int, FileInfo> fileSelection) =>
        _commandCatalog.BuildSuggestions(fileSelection);

    public HomeWorkflowResult Execute(ConsoleInput input, IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        var command = input.FirstWord.ToCommandKey();
        return _commandCatalog.TryExecute(command, input, fileSelection, out var result)
            ? result
            : _fileOpenFallbackProcessor.Execute(_commandContext, input, fileSelection);
    }

    public FileInfo? ResolveFile(IReadOnlyDictionary<int, FileInfo> fileSelection, string fileIdentifier)
    {
        return _appContext.MapSelectionService.FindFile(fileSelection, fileIdentifier);
    }
}
