#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Application;

internal sealed class HomeWorkflow
{
    private readonly FocusAppContext _appContext;

    public HomeWorkflow(FocusAppContext appContext)
    {
        _appContext = appContext;
    }

    public Dictionary<int, FileInfo> GetFileSelection()
    {
        return _appContext.MapSelectionService.GetTopSelection();
    }

    public string BuildHomePageText(IReadOnlyDictionary<int, FileInfo> files)
    {
        const int sampleFileNumber = 1;
        var commandColor = ConfigurationConstants.CommandColor;
        var filesExist = files.Any();
        var homePageMenuTextBuilder = new StringBuilder();

        foreach (var file in files)
        {
            homePageMenuTextBuilder.AppendLine(
                $"[{commandColor}]{AccessibleKeyNumbering.GetStringFor(file.Key)}[!]/[{commandColor}]{file.Key}[!] - {file.Value.NameWithoutExtension()}.");
        }

        homePageMenuTextBuilder.AppendLine();

        if (filesExist)
        {
            homePageMenuTextBuilder.Append(
                $"Type file identifier like \"[{commandColor}]{sampleFileNumber}[!]\" or \"[{commandColor}]{AccessibleKeyNumbering.GetStringFor(sampleFileNumber)}[!]\" to open file.{Environment.NewLine}");
        }

        var updatedVersion = AutoUpdateManager.CheckUpdatedVersion();
        homePageMenuTextBuilder.AppendLine();
        homePageMenuTextBuilder.Append(CommandHelpFormatter.BuildGroupedLines(GetHomeCommandGroups(filesExist, updatedVersion)));

        return homePageMenuTextBuilder.ToString();
    }

    private static IReadOnlyList<CommandHelpGroup> GetHomeCommandGroups(bool filesExist, string? updatedVersion)
    {
        var groups = new List<CommandHelpGroup>
        {
            new("Create", new[] { $"{HomePage.OptionNew} <file name>" }),
            new("Find", new[]
            {
                $"{HomePage.OptionSearch} <query>",
                $"{HomePage.OptionTasks}/{HomePage.OptionTasksAlias} [todo|doing|done|all]",
                HomePage.OptionRefresh
            }),
            new("System", updatedVersion == null
                ? new[] { HomePage.OptionExit }
                : new[] { $"{HomePage.OptionUpdateApp} ({updatedVersion})", HomePage.OptionExit })
        };

        if (filesExist)
        {
            groups.Insert(1, new CommandHelpGroup("Manage", new[]
            {
                $"{HomePage.OptionRen} <file>",
                $"{HomePage.OptionDel} <file>"
            }));
        }

        return groups;
    }

    public HomeWorkflowResult Execute(ConsoleInput input, IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        return input.FirstWord.ToCommandLanguage() switch
        {
            HomePage.OptionExit => HomeWorkflowResult.Exit,
            HomePage.OptionNew => HandleCreateFile(input),
            HomePage.OptionRen => HandleRenameFile(input, fileSelection),
            HomePage.OptionDel => HandleDeleteFile(input, fileSelection),
            HomePage.OptionUpdateApp => HandleUpdateApp(),
            HomePage.OptionSearch => HandleSearch(input),
            HomePage.OptionTasks or HomePage.OptionTasksAlias => HandleTasks(input),
            _ => HandleOpenFile(input, fileSelection)
        };
    }

    public FileInfo? ResolveFile(IReadOnlyDictionary<int, FileInfo> fileSelection, string fileIdentifier)
    {
        return _appContext.MapSelectionService.FindFile(fileSelection, fileIdentifier);
    }

    private static HomeWorkflowResult HandleUpdateApp()
    {
        AutoUpdateManager.HandleUpdate();
        return HomeWorkflowResult.Continue;
    }

    private HomeWorkflowResult HandleCreateFile(ConsoleInput input)
    {
        _appContext.Navigator.OpenCreateMap(input.Parameters, new MindMap(input.Parameters));
        return HomeWorkflowResult.Continue;
    }

    private HomeWorkflowResult HandleDeleteFile(ConsoleInput input, IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        var file = ResolveFile(fileSelection, input.Parameters);
        if (file == null)
            return HomeWorkflowResult.Error($"File \"{input.Parameters}\" wasn't found. Try again.");

        if (new Confirmation($"Are you sure you want to delete: \"{file.Name}\"?").Confirmed())
        {
            _appContext.MapRepository.DeleteMap(file);
        }

        return HomeWorkflowResult.Continue;
    }

    private HomeWorkflowResult HandleOpenFile(ConsoleInput input, IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        var file = ResolveFile(fileSelection, input.FirstWord);
        if (file == null)
            return HomeWorkflowResult.Error($"File \"{input.FirstWord}\" wasn't found. Try again.");

        _appContext.Navigator.OpenEditMap(file.FullName);
        return HomeWorkflowResult.Continue;
    }

    private HomeWorkflowResult HandleRenameFile(ConsoleInput input, IReadOnlyDictionary<int, FileInfo> fileSelection)
    {
        var file = ResolveFile(fileSelection, input.Parameters);
        if (file == null)
            return HomeWorkflowResult.Error($"File \"{input.Parameters}\" wasn't found. Try again.");

        new RenameFileDialog(_appContext.MapRepository, file).Show();
        return HomeWorkflowResult.Continue;
    }

    private HomeWorkflowResult HandleSearch(ConsoleInput input)
    {
        var searchResult = SearchCommandHelper.Run(
            input.Parameters,
            query => MapsSearchService.Search(_appContext.MapRepository, query),
            includeMapName: true);

        if (searchResult.HasError)
            return HomeWorkflowResult.Error(searchResult.ErrorMessage);

        if (searchResult.SelectedResult != null)
        {
            _appContext.Navigator.OpenEditMap(
                searchResult.SelectedResult.MapFilePath,
                searchResult.SelectedResult.NodeId);
        }

        return HomeWorkflowResult.Continue;
    }

    private HomeWorkflowResult HandleTasks(ConsoleInput input)
    {
        if (!TaskCommandHelper.TryParseFilter(input.Parameters, out var filter, out var errorMessage))
            return HomeWorkflowResult.Error(errorMessage!);

        var tasks = TaskQueryService.GetTasks(_appContext.MapRepository, filter);
        if (!tasks.Any())
            return HomeWorkflowResult.Error(TaskCommandHelper.BuildEmptyTasksMessage(filter, acrossAllMaps: true));

        var selectedResult = new SearchResultsPage(
            tasks,
            TaskCommandHelper.GetTasksTitle(filter, acrossAllMaps: true),
            new SearchResultDisplayOptions(
                includeMapName: true,
                colorizeAncestorPath: true,
                highlightTerms: Array.Empty<string>()))
            .SelectResult();

        if (selectedResult != null)
        {
            _appContext.Navigator.OpenEditMap(
                selectedResult.MapFilePath,
                selectedResult.NodeId);
        }

        return HomeWorkflowResult.Continue;
    }
}
