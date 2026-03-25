using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.DialogHelpers;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Edit
{
    internal class EditMapPage : PageWithSuggestedOptions
    {
        private const string AddOption = "add";
        private const string AddIdeaOption = "idea";
        private const string ClearIdeasOption = "clearideas";
        private const string SliceOption = "slice";
        private const string InOption = "in";
        private const string OutOption = "out";
        private const string LinkFromOption = "linkfrom";
        private const string LinkToOption = "linkto";
        private const string OpenLinkOption = "openlink";
        private const string BacklinksOption = "backlinks";
        private const string UpOption = "up";
        private const string GoToOption = "cd";
        private const string GoToOptionSubOptionUp = "..";
        private const string RootOption = "ls";
        private const string DelOption = "del";
        private const string EditOption = "edit";
        private const string HideOption = "min";
        private const string UnhideOption = "max";
        private const string SearchOption = "search";
        private const string ExportOption = "export";
        private const string ExitOption = "exit";

        private readonly string[] _nodeOptions = new[] { GoToOption, EditOption, DelOption, HideOption, UnhideOption, SliceOption, LinkFromOption, LinkToOption };

        private readonly string _filePath;
        private MindMap _map;
        private readonly MapsStorage _mapsStorage;
        private readonly Guid? _initialNodeIdentifier;
        private string _transientMessage;

        public EditMapPage(
            string filePath,
            MapsStorage mapsStorage,
            Guid? initialNodeIdentifier = null)
        {
            _filePath = filePath;
            _mapsStorage = mapsStorage;
            _initialNodeIdentifier = initialNodeIdentifier;
            _map = MapFile.OpenFile(_filePath);
            RefreshGlobalLinkIndex();
            if (_initialNodeIdentifier.HasValue)
            {
                _map.ChangeCurrentNodeById(_initialNodeIdentifier.Value);
            }
        }

        public override void Show()
        {
            Console.Title = Path.GetFileName(_filePath) ?? Console.Title;
            var exit = false;
            Redraw();

            while (!exit)
            {
                var commandResult = ProcessCommand();
                exit = commandResult.ShouldExit;
                if (commandResult.IsSuccess)
                {
                    if (commandResult.ShouldPersist)
                        Save();
                    Redraw(_transientMessage);
                    _transientMessage = null;
                }
                else
                {
                    Redraw(commandResult.ErrorString, isError: true);
                }
            }
        }

        private CommandExecutionResult ProcessCommand()
        {
            var input = GetCommand();
            if (string.IsNullOrWhiteSpace(input.InputString))
                return CommandExecutionResult.Error("Empty command");
            var command = input.FirstWord.ToCommandLanguage();
            var parameters = input.Parameters;

            return command switch
            {
                ExitOption => Exit,
                EditOption => ProcessEdit(parameters),
                AddOption => ProcessAdd(),
                AddIdeaOption => ProcessAddIdea(parameters),
                ClearIdeasOption => ProcessClearIdeas(parameters),
                SliceOption => ProcessSlice(parameters),
                LinkFromOption => ProcessLinkFrom(parameters),
                LinkToOption => ProcessLinkTo(parameters),
                OpenLinkOption => ProcessOpenLink(),
                BacklinksOption => ProcessBacklinks(),
                HideOption => ProcessHide(parameters),
                UnhideOption => ProcessUnhide(parameters),
                SearchOption => ProcessSearch(parameters),
                ExportOption => ProcessExport(),
                GoToOption => ProcessGoTo(parameters),
                DelOption => ProcessCommandDel(parameters),
                RootOption => ProcessGoToRoot(),
                UpOption => ProcessCommandGoUp(),
                _ => ProcessGoToChildOrAddCommandBasedOnTheContent(command, input)
            };
        }

        private CommandExecutionResult ProcessGoToChildOrAddCommandBasedOnTheContent(string command, ConsoleInput input)
        {
            if (!ThereAreSubNodes())
                return ProcessAddCurrentInputString(input);

            var goToChildCommandResult = ProcessCommandGoToChild(command);

            if (goToChildCommandResult.IsSuccess)
                return goToChildCommandResult;

            return new Confirmation($"Did you mean to add new record? \"{input.InputString.GetContentPeek()}\"").Confirmed()
                ? ProcessAddCurrentInputString(input)
                : goToChildCommandResult;
        }

        private CommandExecutionResult ProcessAddCurrentInputString(ConsoleInput input)
        {
            var addNoteDialog = new AddNoteDialog(_map);
            addNoteDialog.ShowWithInitialInput(input.InputString);
            return addNoteDialog.DidAddNodes
                ? CommandExecutionResult.SuccessAndPersist
                : CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessEdit(string parameters)
        {
            var editDialog = new EditDialog(_map, parameters);
            editDialog.Show();
            return editDialog.DidEdit ? CommandExecutionResult.SuccessAndPersist : CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessAdd()
        {
            var addNoteDialog = new AddNoteDialog(_map);
            addNoteDialog.Show();
            return addNoteDialog.DidAddNodes ? CommandExecutionResult.SuccessAndPersist : CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessSearch(string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
                return CommandExecutionResult.Error("Search query is empty");

            var searchResults = MindMapSearchService.Search(_map, parameters, _filePath);
            if (!searchResults.Any())
                return CommandExecutionResult.Error($"No matches for \"{parameters}\"");

            var selectedResult = new SearchResultsPage(
                searchResults,
                $"Search results for \"{parameters}\"",
                includeMapName: false)
                .SelectResult();

            if (selectedResult == null)
                return CommandExecutionResult.Success;

            return _map.ChangeCurrentNodeById(selectedResult.NodeId)
                ? CommandExecutionResult.Success
                : CommandExecutionResult.Error("Couldn't open selected result");
        }

        private CommandExecutionResult ProcessExport()
        {
            var exportPage = new ExportPage(
                MapFileHelper.SanitizeFileName(_map.GetCurrentNodeName(), fallbackFileName: "export"));
            exportPage.Show();

            if (exportPage.SelectedExport == null)
                return CommandExecutionResult.Success;

            return ProcessExport(exportPage.SelectedExport);
        }

        private CommandExecutionResult ProcessExport(ExportFormat format, IEnumerable<string> parameterWords)
        {
            var exportRequest = ParseExportArguments(format, parameterWords);
            return ProcessExport(exportRequest);
        }

        private CommandExecutionResult ProcessExport(ExportRequest exportRequest)
        {
            var exportDirectory = Path.GetDirectoryName(_filePath);
            if (string.IsNullOrWhiteSpace(exportDirectory))
                return CommandExecutionResult.Error("Couldn't determine export folder");

            var targetFileName = MapFileHelper.SanitizeFileName(
                string.IsNullOrWhiteSpace(exportRequest.FileName)
                    ? _map.GetCurrentNodeName()
                    : exportRequest.FileName,
                fallbackFileName: "export");

            try
            {
                var exportedContent = MapExportService.Export(
                    _map.GetCurrentNode(),
                    exportRequest.Format,
                    new NodeExportOptions(exportRequest.SkipCollapsedDescendants));
                string exportedFilePath = null;

                new RequestRenameUntilFileNameIsAvailableDialog(
                    exportDirectory,
                    targetFileName,
                    filePath =>
                    {
                        File.WriteAllText(filePath, exportedContent);
                        exportedFilePath = filePath;
                    },
                    exportRequest.Format.GetFileExtension())
                    .Show();

                _mapsStorage.Sync();
                var collapsedSuffix = exportRequest.SkipCollapsedDescendants ? " (collapsed descendants skipped)" : string.Empty;
                _transientMessage =
                    $"Exported {exportRequest.Format.ToExportVerb()} to \"{Path.GetFileName(exportedFilePath)}\"{collapsedSuffix}";
                return CommandExecutionResult.Success;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return CommandExecutionResult.Error($"Couldn't export {exportRequest.Format.ToExportVerb()}: {ex.Message}");
            }
        }

        private static ExportRequest ParseExportArguments(ExportFormat format, IEnumerable<string> parameterWords)
        {
            var fileNameParts = new List<string>();
            var skipCollapsedDescendants = false;

            foreach (var parameterWord in parameterWords.Where(word => !string.IsNullOrWhiteSpace(word)))
            {
                if (parameterWord is "-c" or "--collapsed")
                {
                    skipCollapsedDescendants = true;
                    continue;
                }

                fileNameParts.Add(parameterWord);
            }

            return new ExportRequest(
                format,
                string.Join(' ', fileNameParts),
                skipCollapsedDescendants);
        }

        private CommandExecutionResult ProcessOpenLink()
        {
            var currentNode = _map.GetCurrentNode();
            var links = LinkNavigationService.GetOutgoingLinks(currentNode);
            if (!links.Any())
                return CommandExecutionResult.Error("Current node has no links");

            return NavigateToLinkedNode(links, "Linked nodes");
        }

        private CommandExecutionResult ProcessBacklinks()
        {
            var currentNode = _map.GetCurrentNode();
            var backlinks = LinkNavigationService.GetBacklinks(currentNode);
            if (!backlinks.Any())
                return CommandExecutionResult.Error("Current node has no backlinks");

            return NavigateToLinkedNode(backlinks, "Backlinks");
        }

        private CommandExecutionResult NavigateToLinkedNode(IReadOnlyList<NodeSearchResult> relatedNodes, string title)
        {
            var selectedResult = new SearchResultsPage(relatedNodes, title, includeMapName: true)
                .SelectResult();

            if (selectedResult == null)
                return CommandExecutionResult.Success;

            if (string.Equals(selectedResult.MapFilePath, _filePath, StringComparison.InvariantCultureIgnoreCase))
            {
                return _map.ChangeCurrentNodeById(selectedResult.NodeId)
                    ? CommandExecutionResult.Success
                    : CommandExecutionResult.Error("Couldn't open selected node");
            }

            new EditMapPage(selectedResult.MapFilePath, _mapsStorage, selectedResult.NodeId).Show();
            return CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessSlice(string parameters)
        {
            var selectionMenu = new SelectionMenu(new List<string>() { InOption, OutOption });
            selectionMenu.Show();
            var selectedOption = selectionMenu.GetSelectedOption();

            return selectedOption switch
            {
                1 => ProcessAttach(),
                2 => ProcessDetach(parameters),
                _ => CommandExecutionResult.Error("Operation Canceled")
            };
        }

        private CommandExecutionResult ProcessAttach()
        {
            var attachMode = new AttachMode(_map, _mapsStorage);
            attachMode.Show();
            return attachMode.DidAttachMap
                ? CommandExecutionResult.SuccessAndPersist
                : CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessAddIdea(string parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                var addIdeaDialog = new AddIdeaDialog(_map);
                addIdeaDialog.ShowWithInitialInput(parameters);
                return addIdeaDialog.DidAddIdeas ? CommandExecutionResult.SuccessAndPersist : CommandExecutionResult.Success;
            }

            var dialog = new AddIdeaDialog(_map);
            dialog.Show();
            return dialog.DidAddIdeas ? CommandExecutionResult.SuccessAndPersist : CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessClearIdeas(string parameters)
        {
            var nodeIdentifier = parameters;

            if (!string.IsNullOrWhiteSpace(nodeIdentifier))
            {
                if (!_map.HasNode(nodeIdentifier))
                    return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");

                var nodeContentPreview = _map.GetNodeContentPeekByIdentifier(nodeIdentifier);

                if (!new Confirmation($"Clear idea tags for: \"{nodeIdentifier}\". \"{nodeContentPreview}\"?").Confirmed())
                    return CommandExecutionResult.Error("Cancelled!");

                return _map.DeleteNodeIdeaTags(nodeIdentifier)
                    ? CommandExecutionResult.SuccessAndPersist
                    : CommandExecutionResult.Error($"Couldn't remove \"{nodeIdentifier}\"");
            }


            if (!new Confirmation("Clear idea tags for current node?").Confirmed())
                return CommandExecutionResult.Error("Cancelled!");

            return _map.DeleteCurrentNodeIdeaTags()
                ? CommandExecutionResult.SuccessAndPersist
                : CommandExecutionResult.Error("Can't delete current node (report a bug)");

        }

        private static CommandExecutionResult Exit => CommandExecutionResult.ExitCommand;

        private CommandExecutionResult ProcessGoToRoot()
        {
            if (_map.GoToRoot())
            {
                return CommandExecutionResult.Success;
            }
            return CommandExecutionResult.Error("Can't go to root");
        }

        private CommandExecutionResult ProcessGoTo(string parameters)
        {
            return parameters == GoToOptionSubOptionUp
                ? ProcessCommandGoUp()
                : ProcessCommandGoToChild(parameters);
        }

        private CommandExecutionResult ProcessDetach(string parameters)
        {
            var nodeIdentifier = parameters;
            if (string.IsNullOrWhiteSpace(nodeIdentifier))
            {
                var nodeContentPreview = _map.GetCurrentNodeContentPeek();

                if (new Confirmation($"Detach current node? {nodeContentPreview}").Confirmed())
                {
                    _map.DetachCurrentNode(_mapsStorage);
                    return CommandExecutionResult.SuccessAndPersist;
                }

                return CommandExecutionResult.Error("Cancelled");
            }

            if (!_map.HasNode(nodeIdentifier))
            {
                return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");
            }

            _map.DetachNode(_mapsStorage, nodeIdentifier);

            return CommandExecutionResult.SuccessAndPersist;
        }

        private CommandExecutionResult ProcessLinkTo(string parameters)
        {
            if (!GlobalLinkDitionary.NodesToBeLinked.Any())
                return CommandExecutionResult.Error("No source node selected. Use linkfrom first");

            var nodeIdentifier = parameters;
            if (!_map.HasNode(nodeIdentifier))
                return CommandExecutionResult.Error($"Couldn't find node \"{nodeIdentifier}\"");

            var targetNodePeek = _map.GetNodeContentPeekByIdentifier(nodeIdentifier);

            var nodeToLinkFrom = GlobalLinkDitionary.NodesToBeLinked.Peek();
            var selectedRelationType = SelectLinkRelationType();
            if (!selectedRelationType.HasValue)
                return CommandExecutionResult.Error("Cancelled!");

            if (!new Confirmation(
                    $"Link \"{nodeToLinkFrom.Name}\" to \"{targetNodePeek}\" as \"{selectedRelationType.Value.ToDisplayString()}\"?")
                .Confirmed())
                return CommandExecutionResult.Error("Cancelled!");

            var linkResult = _map.LinkToNode(
                nodeIdentifier,
                nodeToLinkFrom,
                selectedRelationType.Value,
                "Link Metadata");
            if (!linkResult)
                return CommandExecutionResult.Error($"Unexpected result while linking \"{nodeIdentifier}\"");

            GlobalLinkDitionary.NodesToBeLinked.Pop();
            return CommandExecutionResult.SuccessAndPersist;
        }
        private CommandExecutionResult ProcessLinkFrom(string parameters)
        {
            var nodeIdentifier = parameters;
            _map.AddNodeToLinkStack(nodeIdentifier);

            return CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessHide(string parameters)
        {
            var nodeIdentifier = parameters;
            if (_map.HideNode(nodeIdentifier))
            {
                return CommandExecutionResult.SuccessAndPersist;
            }
            return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");
        }

        private CommandExecutionResult ProcessUnhide(string parameters)
        {
            var nodeIdentifier = parameters;
            if (_map.UnhideNode(nodeIdentifier))
            {
                return CommandExecutionResult.SuccessAndPersist;
            }
            return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");
        }

        private CommandExecutionResult ProcessCommandDel(string parameters)
        {
            var nodeIdentifier = parameters;

            if (!string.IsNullOrWhiteSpace(nodeIdentifier))
            {
                if (!_map.HasNode(nodeIdentifier))
                    return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");

                var nodeContentPreview = _map.GetNodeContentPeekByIdentifier(nodeIdentifier);

                if (!new Confirmation($"Are you sure to delete \"{nodeIdentifier}\". \"{nodeContentPreview}\"").Confirmed())
                    return CommandExecutionResult.Error("Cancelled!");

                return _map.DeleteChildNode(nodeIdentifier)
                    ? CommandExecutionResult.SuccessAndPersist
                    : CommandExecutionResult.Error($"Couldn't remove \"{nodeIdentifier}\"");
            }

            var contentPeek = _map.GetCurrentNodeContentPeek();
            var nodeToDelete = _map.GetCurrentNodeName();

            if (_map.IsAtRootNode() || !_map.GoUp())
                return CommandExecutionResult.Error("Can't remove root node");

            if (!new Confirmation($"Are you sure to delete current node:{contentPeek}").Confirmed())
                return CommandExecutionResult.Error("Cancelled!");

            return _map.DeleteChildNode(nodeToDelete)
                ? CommandExecutionResult.SuccessAndPersist
                : CommandExecutionResult.Error("Can't delete current node (report a bug)");
        }

        private CommandExecutionResult ProcessCommandGoToChild(string parameters)
        {
            var nodeIdentifier = parameters;

            if (ProcessCommandInvariant(_map.ChangeCurrentNode, nodeIdentifier))
            {
                return CommandExecutionResult.Success;
            }
            return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");
        }

        private CommandExecutionResult ProcessCommandGoUp()
        {
            if (_map.GoUp())
            {
                return CommandExecutionResult.Success;
            }
            return Exit;
        }

        private bool ThereAreSubNodes() => _map.GetChildren().Any();

        private void Redraw(string message = null, bool isError = false)
        {
            var newConsoleContent = _map.GetCurrentSubtreeString();
            var commandOptions = GetCommandOptions();
            var screenBuilder = new System.Text.StringBuilder(newConsoleContent.Length + 256);

            screenBuilder.Append(newConsoleContent);

            if (!string.IsNullOrEmpty(message))
            {
                var messagePrefix = isError ? ":!" : ":i";
                screenBuilder.Append($"{messagePrefix} {message}{Environment.NewLine}{Environment.NewLine}");
            }

            screenBuilder.Append($":> {string.Join("; ", commandOptions)}{Environment.NewLine}");
            if (GlobalLinkDitionary.NodesToBeLinked.Any())
            {
                screenBuilder.Append($":Nodes to be linked> {string.Join("; ", GlobalLinkDitionary.NodesToBeLinked.Select(n => n.Name))}{Environment.NewLine}");
            }

            //TODO: Refactor: extract console manipulations to wrapper class and handle ioexception
            try
            {
                Console.Clear();
                if (OsInfo.IsWindows())
                {
                    Console.Write("\x1b[3J");
                }
            }
            catch (IOException e)
            {
                Console.Beep();
            }
            ColorfulConsole.Write(screenBuilder.ToString());
        }

        private void Save()
        {
            _map.SaveTo(_filePath);
            RefreshGlobalLinkIndex();
            _mapsStorage.Sync();
        }

        private string[] GetCommandOptions() =>
            new[]
            {
                AddOption,
                AddIdeaOption,
                ClearIdeasOption,
                SliceOption,
                LinkFromOption,
                LinkToOption,
                OpenLinkOption,
                BacklinksOption,
                HideOption,
                UnhideOption,
                SearchOption,
                ExportOption,
                ExitOption,
                GoToOption,
                EditOption,
                DelOption,
                UpOption,
                RootOption
            }
                .Union(_map.GetChildren().Keys.Select(k => k.ToString()))
                .Union(_map.GetChildren().Keys.Select(k => AccessibleKeyNumbering.GetStringFor(k)))
                .ToArray();

        protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
        {
            var childNodes = _map.GetChildren();
            if (!childNodes.Any())
                return GetCommandOptions();

            //TODO: Use similar approach in homePage
            return GetCommandOptions()
                    .Union(childNodes.Keys.Select(k => k.ToString()))
                    .Union(childNodes.Values.Select(value => $"{SearchOption} {value}"))
                    .Union(_nodeOptions.SelectMany(opt => childNodes.Keys.Select(k => $"{opt} {k}")))
                    .Union(_nodeOptions.SelectMany(opt => childNodes.Values.Select(k => $"{opt} {k}")));
        }

        private void RefreshGlobalLinkIndex()
        {
            MapFile.RebuildNodeIndex(_mapsStorage.GetAll().Select(file => file.FullName));
        }

        private static LinkRelationType? SelectLinkRelationType()
        {
            var supportedTypes = LinkRelationTypeExtensions.GetSupportedTypes();
            var selectionMenu = new SelectionMenu(supportedTypes.Select(type => type.ToDisplayString()).ToArray());
            selectionMenu.Show();

            var selectedOption = selectionMenu.GetSelectedOption();
            if (selectedOption <= 0 || selectedOption > supportedTypes.Length)
                return null;

            return supportedTypes[selectedOption - 1];
        }
    }
}
