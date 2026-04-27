#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages;
using Systems.Sanity.Focus.Pages.Edit;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.DialogHelpers;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Application;

internal sealed class EditWorkflow
{
    private const string AddOption = "add";
    private const string AddBlockOption = "addblock";
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
    private const string TodoOption = "todo";
    private const string TodoAliasOption = "td";
    private const string DoingOption = "doing";
    private const string DoneOption = "done";
    private const string DoneAliasOption = "dn";
    private const string NoTaskOption = "notask";
    private const string ToggleTaskOption = "toggle";
    private const string ToggleTaskAliasOption = "tg";
    private const string HideDoneTasksOption = "hidedone";
    private const string ShowDoneTasksOption = "showdone";
    private const string TasksOption = "tasks";
    private const string TasksAliasOption = "ts";
    private const string CaptureOption = "capture";
    private const string MetaOption = "meta";
    private const string AttachmentsOption = "attachments";
    private const string ExportOption = "export";
    private const string ExitOption = "exit";
    private const int ClipboardTextPreviewMinLength = 20;
    private const int ClipboardTextPreviewMaxLength = 30;

    private readonly string[] _nodeOptions =
    {
        GoToOption,
        EditOption,
        DelOption,
        HideOption,
        UnhideOption,
        SliceOption,
        LinkFromOption,
        LinkToOption,
        TodoOption,
        TodoAliasOption,
        DoingOption,
        DoneOption,
        DoneAliasOption,
        NoTaskOption,
        ToggleTaskOption,
        ToggleTaskAliasOption,
        HideDoneTasksOption,
        ShowDoneTasksOption,
        MetaOption
    };

    private readonly FocusAppContext _appContext;
    private string _filePath;
    private MindMap _map;

    public EditWorkflow(string filePath, FocusAppContext appContext, Guid? initialNodeIdentifier = null)
    {
        _filePath = filePath;
        _appContext = appContext;
        _map = _appContext.MapRepository.OpenMapForEditing(filePath);
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

        screenBuilder.Append(BuildCurrentAttachmentSummary());

        if (showCommands)
        {
            screenBuilder.Append(BuildCommandHelpText());
        }
        else
        {
            screenBuilder.Append($":i Commands hidden. Press \"~\" to show.{Environment.NewLine}");
        }

        return screenBuilder.ToString();
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

        helpGroups.Add(new CommandHelpGroup("Navigate", new[]
        {
            $"{GoToOption} <child>",
            UpOption,
            RootOption
        }));
        helpGroups.Add(new CommandHelpGroup("Edit", new[]
        {
            AddOption,
            AddBlockOption,
            AddIdeaOption,
            $"{EditOption} [child]",
            $"{DelOption} [child]",
            $"{ClearIdeasOption} [child]",
            $"{SliceOption} [child]",
            $"{HideOption} <child>",
            $"{UnhideOption} <child>",
            CaptureOption
        }));
        helpGroups.Add(new CommandHelpGroup("To Do", new[]
        {
            $"{TodoOption}/{TodoAliasOption} [child]",
            $"{DoingOption} [child]",
            $"{DoneOption}/{DoneAliasOption} [child]",
            $"{NoTaskOption} [child]",
            $"{ToggleTaskOption}/{ToggleTaskAliasOption} [child]",
            $"{HideDoneTasksOption} [child]",
            $"{ShowDoneTasksOption} [child]",
            $"{TasksOption}/{TasksAliasOption} [todo|doing|done|all]"
        }));
        helpGroups.Add(new CommandHelpGroup("Links", new[]
        {
            $"{LinkFromOption} <child>",
            $"{LinkToOption} <child>",
            OpenLinkOption,
            BacklinksOption
        }));
        helpGroups.Add(new CommandHelpGroup("Search/Export", new[]
        {
            $"{SearchOption} <query>",
            ExportOption,
            $"{MetaOption} [child]",
            $"{AttachmentsOption} [attachment]"
        }));
        helpGroups.Add(new CommandHelpGroup("System", new[]
        {
            ExitOption
        }));

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
                ExitOption => CommandExecutionResult.ExitCommand,
                EditOption => ProcessEdit(parameters),
                AddOption => ProcessAdd(),
                AddBlockOption => ProcessAddBlock(),
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
                TodoOption or TodoAliasOption => ProcessSetTaskState(parameters, TaskState.Todo),
                DoingOption => ProcessSetTaskState(parameters, TaskState.Doing),
                DoneOption or DoneAliasOption => ProcessSetTaskState(parameters, TaskState.Done),
                NoTaskOption => ProcessSetTaskState(parameters, TaskState.None),
                ToggleTaskOption or ToggleTaskAliasOption => ProcessToggleTaskState(parameters),
                HideDoneTasksOption => ProcessSetHideDoneTasks(parameters, hideDoneTasks: true),
                ShowDoneTasksOption => ProcessSetHideDoneTasks(parameters, hideDoneTasks: false),
                TasksOption or TasksAliasOption => ProcessTasks(parameters),
                CaptureOption => ProcessCapture(),
                MetaOption => ProcessMeta(parameters),
                AttachmentsOption => ProcessAttachments(parameters),
                ExportOption => ProcessExport(),
                GoToOption => ProcessGoTo(parameters),
                DelOption => ProcessCommandDel(parameters),
                RootOption => ProcessGoToRoot(),
                UpOption => ProcessCommandGoUp(),
                _ => ProcessGoToChildOrAddCommandBasedOnTheContent(command, input)
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
        if (!childNodes.Any())
            return GetCommandOptions()
                .Union(TaskCommandHelper.GetTaskListSuggestions(TasksOption, TasksAliasOption));

        return GetCommandOptions()
            .Union(childNodes.Values.Select(value => $"{SearchOption} {value}"))
            .Union(TaskCommandHelper.GetTaskListSuggestions(TasksOption, TasksAliasOption))
            .Union(_nodeOptions.SelectMany(option => childNodes.Keys.Select(key => $"{option} {key}")))
            .Union(_nodeOptions.SelectMany(option => childNodes.Values.Select(value => $"{option} {value}")));
    }

    public void Save(string commitMessage)
    {
        if (string.IsNullOrWhiteSpace(commitMessage))
            throw new ArgumentException("Sync commit message is required.", nameof(commitMessage));

        _appContext.MapRepository.SaveMap(_filePath, _map);
        _appContext.RefreshLinkIndex();
        _appContext.MapsStorage.Sync(commitMessage);
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
        var currentAttachmentSelectors = GetCurrentAttachmentSelectors().ToArray();
        var bareAttachmentSelectors = currentAttachmentSelectors
            .Where(selector => !childSelectors.Contains(selector));

        return new[]
            {
                AddOption,
                AddBlockOption,
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
                TodoOption,
                TodoAliasOption,
                DoingOption,
                DoneOption,
                DoneAliasOption,
                NoTaskOption,
                ToggleTaskOption,
                ToggleTaskAliasOption,
                HideDoneTasksOption,
                ShowDoneTasksOption,
                TasksOption,
                TasksAliasOption,
                CaptureOption,
                MetaOption,
                AttachmentsOption,
                ExportOption,
                ExitOption,
                GoToOption,
                EditOption,
                DelOption,
                UpOption,
                RootOption
            }
            .Union(childNodes.Keys.Select(key => key.ToString()))
            .Union(childNodes.Keys.Select(AccessibleKeyNumbering.GetStringFor))
            .Union(bareAttachmentSelectors)
            .Union(currentAttachmentSelectors.Select(selector => $"{AttachmentsOption} {selector}"))
            .ToArray();
    }

    private CommandExecutionResult ProcessAdd()
    {
        var addNoteDialog = new AddNoteDialog(_map);
        addNoteDialog.Show();
        return addNoteDialog.DidAddNodes
            ? PersistMapChange("Add note")
            : CommandExecutionResult.Success();
    }

    private CommandExecutionResult ProcessAddBlock()
    {
        var addBlockDialog = new AddBlockDialog(_map);
        addBlockDialog.Show();
        return addBlockDialog.DidAddBlock
            ? PersistMapChange("Add block")
            : CommandExecutionResult.Success();
    }

    private CommandExecutionResult ProcessAddCurrentInputString(ConsoleInput input)
    {
        var addNoteDialog = new AddNoteDialog(_map);
        addNoteDialog.ShowWithInitialInput(input.InputString);
        return addNoteDialog.DidAddNodes
            ? PersistMapChange("Add note")
            : CommandExecutionResult.Success();
    }

    private CommandExecutionResult ProcessAddIdea(string parameters)
    {
        var dialog = new AddIdeaDialog(_map);
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            dialog.ShowWithInitialInput(parameters);
        }
        else
        {
            dialog.Show();
        }

        return dialog.DidAddIdeas
            ? PersistMapChange("Add idea")
            : CommandExecutionResult.Success();
    }

    private CommandExecutionResult ProcessAttach()
    {
        var attachMode = new AttachMode(_map, _appContext, _filePath);
        attachMode.Show();
        return attachMode.DidAttachMap
            ? PersistMapChange("Attach map", relation: "into")
            : CommandExecutionResult.Success();
    }

    private CommandExecutionResult ProcessAttachments(string parameters)
    {
        try
        {
            var attachments = GetCurrentAttachments();
        if (attachments.Count == 0)
            return CommandExecutionResult.Error("Current node has no attachments");

        if (string.IsNullOrWhiteSpace(parameters))
        {
            return attachments.Count == 1
                ? OpenAttachment(attachments[0])
                : CommandExecutionResult.Error(
                    $"Specify attachment shortcut: {string.Join(", ", GetCurrentAttachmentSelectors())}");
        }

        return TryResolveCurrentAttachment(parameters, out var attachment, out var errorMessage)
            ? OpenAttachment(attachment!)
            : CommandExecutionResult.Error(errorMessage!);
        }
        catch (Exception ex)
        {
            return CommandExecutionResult.Error(ExceptionDiagnostics.LogException(ex, "opening attachment"));
        }
    }

    private CommandExecutionResult ProcessBacklinks()
    {
        var backlinks = _appContext.LinkNavigationService.GetBacklinks(_map.GetCurrentNode());
        if (!backlinks.Any())
            return CommandExecutionResult.Error("Current node has no backlinks");

        var sourceText = _map.GetCurrentNodeContentPeek();
        return NavigateToLinkedNode(backlinks, $"Backlinks — \"{sourceText}\"");
    }

    private CommandExecutionResult ProcessClearIdeas(string parameters)
    {
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            if (!_map.HasNode(parameters))
                return CommandExecutionResult.Error($"Can't find \"{parameters}\"");

            var nodeContentPreview = _map.GetNodeContentPeekByIdentifier(parameters);
            if (!new Confirmation($"Clear idea tags for: \"{parameters}\". \"{nodeContentPreview}\"?").Confirmed())
                return CommandExecutionResult.Error("Cancelled!");

            return _map.DeleteNodeIdeaTags(parameters)
                ? PersistMapChange("Clear ideas")
                : CommandExecutionResult.Error($"Couldn't remove \"{parameters}\"");
        }

        if (!new Confirmation("Clear idea tags for current node?").Confirmed())
            return CommandExecutionResult.Error("Cancelled!");

        return _map.DeleteCurrentNodeIdeaTags()
            ? PersistMapChange("Clear ideas")
            : CommandExecutionResult.Error("Can't delete current node (report a bug)");
    }

    private CommandExecutionResult ProcessCommandDel(string parameters)
    {
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            if (!_map.HasNode(parameters))
                return CommandExecutionResult.Error($"Can't find \"{parameters}\"");

            var nodeContentPreview = _map.GetNodeContentPeekByIdentifier(parameters);
            if (!new Confirmation($"Are you sure to delete \"{parameters}\". \"{nodeContentPreview}\"").Confirmed())
                return CommandExecutionResult.Error("Cancelled!");

            return _map.DeleteChildNode(parameters)
                ? PersistMapChange("Delete node")
                : CommandExecutionResult.Error($"Couldn't remove \"{parameters}\"");
        }

        var contentPeek = _map.GetCurrentNodeContentPeek();
        var nodeToDelete = _map.GetCurrentNodeName();
        if (_map.IsAtRootNode() || !_map.GoUp())
            return CommandExecutionResult.Error("Can't remove root node");

        if (!new Confirmation($"Are you sure to delete current node:{contentPeek}").Confirmed())
            return CommandExecutionResult.Error("Cancelled!");

        return _map.DeleteChildNode(nodeToDelete)
            ? PersistMapChange("Delete node")
            : CommandExecutionResult.Error("Can't delete current node (report a bug)");
    }

    private CommandExecutionResult ProcessCommandGoToChild(string parameters)
    {
        return InvokeLocalized(_map.ChangeCurrentNode, parameters)
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.Error($"Can't find \"{parameters}\"");
    }

    private CommandExecutionResult ProcessCommandGoUp()
    {
        return _map.GoUp()
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.ExitCommand;
    }

    private CommandExecutionResult ProcessDetach(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            if (!new Confirmation($"Detach current node? {_map.GetCurrentNodeContentPeek()}").Confirmed())
                return CommandExecutionResult.Error("Cancelled");

            var detachedMap = _map.DetachCurrentNodeAsNewMap();
            if (detachedMap == null)
                return CommandExecutionResult.Error("Can't detach root node");

            _appContext.Navigator.OpenCreateMap(detachedMap.RootNode.Name, detachedMap, _filePath);
            return PersistMapChange("Detach node", relation: "from");
        }

        if (!_map.HasNode(parameters))
            return CommandExecutionResult.Error($"Can't find \"{parameters}\"");

        var mapToDetach = _map.DetachNodeAsNewMap(parameters);
        if (mapToDetach == null)
            return CommandExecutionResult.Error($"Couldn't detach \"{parameters}\"");

        _appContext.Navigator.OpenCreateMap(mapToDetach.RootNode.Name, mapToDetach, _filePath);
        return PersistMapChange("Detach node", relation: "from");
    }

    private CommandExecutionResult ProcessEdit(string parameters)
    {
        var didEdit = false;
        if (_map.GetCurrentNode().NodeType == NodeType.TextBlockItem)
        {
            var editBlockDialog = new EditBlockDialog(_map);
            editBlockDialog.Show();
            didEdit = editBlockDialog.DidEdit;
        }
        else
        {
            var editDialog = new EditDialog(_map, parameters);
            editDialog.Show();
            didEdit = editDialog.DidEdit;
        }

        if (!didEdit)
            return CommandExecutionResult.Success();

        if (_map.IsAtRootNode())
            RenameFileToMatchRootNode();

        return PersistMapChange("Edit node");
    }

    private void RenameFileToMatchRootNode()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory == null)
            return;

        var newBaseName = MapFileHelper.SanitizeFileName(_map.RootNode.Name);
        var newFilePath = MapFileHelper.GetFullFilePath(directory, newBaseName);

        if (string.Equals(_filePath, newFilePath, StringComparison.OrdinalIgnoreCase))
            return;

        var candidatePath = newFilePath;
        var counter = 2;
        while (File.Exists(candidatePath) && !string.Equals(candidatePath, _filePath, StringComparison.OrdinalIgnoreCase))
        {
            var candidate = $"{newBaseName}_({counter})";
            candidatePath = MapFileHelper.GetFullFilePath(directory, candidate);
            counter++;
        }

        _appContext.MapRepository.MoveMap(_filePath, candidatePath);
        _filePath = candidatePath;
    }

    private CommandExecutionResult ProcessExport()
    {
        try
        {
            var exportPage = new ExportPage(MapFileHelper.SanitizeFileName(_map.GetCurrentNodeName(), fallbackFileName: "export"));
            exportPage.Show();

            if (exportPage.SelectedExport == null)
                return CommandExecutionResult.Success();

            return ProcessExport(exportPage.SelectedExport);
        }
        catch (Exception ex)
        {
            return CommandExecutionResult.Error(ExceptionDiagnostics.LogException(ex, "exporting map"));
        }
    }

    private CommandExecutionResult ProcessExport(ExportRequest exportRequest)
    {
        if (exportRequest.Destination == ExportDestination.ClipboardText)
            return ProcessCopyTextExport(exportRequest);

        var exportDirectory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(exportDirectory))
            return CommandExecutionResult.Error("Couldn't determine export folder");

        var targetFileName = MapFileHelper.SanitizeFileName(
            string.IsNullOrWhiteSpace(exportRequest.FileName) ? _map.GetCurrentNodeName() : exportRequest.FileName,
            fallbackFileName: "export");

        try
        {
            string? exportedFilePath = null;

            new RequestRenameUntilFileNameIsAvailableDialog(
                exportDirectory,
                targetFileName,
                filePath =>
                {
                    var exportedContent = MapExportService.Export(
                        _map.GetCurrentNode(),
                        exportRequest.Format,
                        new NodeExportOptions(
                            SkipCollapsedDescendants: exportRequest.SkipCollapsedDescendants,
                            UseBlackBackground: exportRequest.UseBlackBackground,
                            IncludeAttachments: exportRequest.IncludeAttachments,
                            MapFilePath: _filePath,
                            ExportFilePath: filePath));
                    File.WriteAllText(filePath, exportedContent);
                    exportedFilePath = filePath;
                },
                exportRequest.Format.GetFileExtension())
                .Show();

            if (exportedFilePath == null)
                return CommandExecutionResult.Success("Export cancelled");

            _appContext.MapsStorage.Sync(BuildExportSyncCommitMessage(exportRequest.Format));
            var collapsedSuffix = exportRequest.SkipCollapsedDescendants ? " (collapsed descendants skipped)" : string.Empty;
            return CommandExecutionResult.Success(
                $"Exported {exportRequest.Format.ToExportVerb()} to \"{Path.GetFileName(exportedFilePath)}\"{collapsedSuffix}");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            return CommandExecutionResult.Error(ExceptionDiagnostics.LogException(ex, "exporting map"));
        }
    }

    private CommandExecutionResult ProcessCopyTextExport(ExportRequest exportRequest)
    {
        var exportedContent = MapExportService.Export(
            _map.GetCurrentNode(),
            ExportFormat.PlainText,
            new NodeExportOptions(SkipCollapsedDescendants: exportRequest.SkipCollapsedDescendants));
        var copyResult = _appContext.ClipboardTextWriter.CopyText(exportedContent);
        if (!copyResult.IsSuccess)
            return CommandExecutionResult.Error(copyResult.ErrorMessage ?? "Couldn't copy text to clipboard.");

        var collapsedSuffix = exportRequest.SkipCollapsedDescendants ? " (collapsed descendants skipped)" : string.Empty;
        return CommandExecutionResult.Success($"Copied plain text export to clipboard{collapsedSuffix}");
    }

    private CommandExecutionResult ProcessGoTo(string parameters)
    {
        return parameters == GoToOptionSubOptionUp
            ? ProcessCommandGoUp()
            : ProcessCommandGoToChild(parameters);
    }

    private CommandExecutionResult ProcessCapture()
    {
        try
        {
            var captureResult = _appContext.ClipboardCaptureService.Capture();
            if (!captureResult.IsSuccess)
                return CommandExecutionResult.Error(captureResult.ErrorMessage ?? "Clipboard capture failed");

            var currentNode = _map.GetCurrentNode();
            var currentNodeIdentifier = GetRequiredNodeIdentifier(currentNode);

            if (captureResult.Kind == ClipboardCaptureKind.Image)
            {
                var timestampUtc = DateTimeOffset.UtcNow;
                var attachment = _appContext.MapsStorage.AttachmentStore.SavePngAttachment(
                    _filePath,
                    currentNodeIdentifier,
                    captureResult.ImageBytes ?? Array.Empty<byte>(),
                    BuildClipboardImageAttachmentDisplayName(timestampUtc),
                    timestampUtc);
                currentNode.AddAttachment(attachment, timestampUtc);

                return PersistMapChange(
                    "Capture clipboard",
                    message: $"Captured clipboard image into \"{NodeDisplayHelper.GetContentPeek(currentNode.Name)}\"");
            }

            var capturedAtUtc = DateTimeOffset.UtcNow;
            var capturedText = captureResult.Text ?? string.Empty;
            var textAttachment = _appContext.MapsStorage.AttachmentStore.SaveTextAttachment(
                _filePath,
                currentNodeIdentifier,
                capturedText,
                BuildClipboardTextAttachmentDisplayName(capturedText, capturedAtUtc),
                capturedAtUtc);
            currentNode.AddAttachment(textAttachment, capturedAtUtc);

            return PersistMapChange(
                "Capture clipboard",
                message: $"Captured clipboard text into \"{NodeDisplayHelper.GetContentPeek(currentNode.Name)}\"");
        }
        catch (Exception ex)
        {
            return CommandExecutionResult.Error(ExceptionDiagnostics.LogException(ex, "capturing clipboard"));
        }
    }

    private CommandExecutionResult ProcessGoToChildOrAddCommandBasedOnTheContent(string command, ConsoleInput input)
    {
        if (!_map.GetChildren().Any())
        {
            if (TryOpenCurrentAttachmentShortcut(command, out var directAttachmentResult))
                return directAttachmentResult;

            return ProcessAddCurrentInputString(input);
        }

        var goToChildCommandResult = ProcessCommandGoToChild(command);
        if (goToChildCommandResult.IsSuccess)
            return goToChildCommandResult;

        if (TryOpenCurrentAttachmentShortcut(command, out var attachmentResult))
            return attachmentResult;

        return new Confirmation($"Did you mean to add new record? \"{input.InputString.GetContentPeek()}\"").Confirmed()
            ? ProcessAddCurrentInputString(input)
            : goToChildCommandResult;
    }

    private CommandExecutionResult ProcessGoToRoot()
    {
        var reloadErrorMessage = PullAndReloadIfChanged();
        if (!string.IsNullOrWhiteSpace(reloadErrorMessage))
            return CommandExecutionResult.Error(reloadErrorMessage);

        return _map.GoToRoot()
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.Error("Can't go to root");
    }

    private string? PullAndReloadIfChanged()
    {
        try
        {
            var fileInfo = new FileInfo(_filePath);
            if (!fileInfo.Exists)
                return null;

            var beforeWrite = fileInfo.LastWriteTimeUtc;
            var beforeLength = fileInfo.Length;

            var result = _appContext.MapsStorage.PullLatestAtStartup();
            if (result.Status != Infrastructure.FileSynchronization.StartupSyncStatus.Succeeded)
                return null;

            fileInfo.Refresh();
            if (fileInfo.LastWriteTimeUtc != beforeWrite || fileInfo.Length != beforeLength)
            {
                _map = _appContext.MapRepository.OpenMapForEditing(_filePath);
                _appContext.RefreshLinkIndex();
            }

            return null;
        }
        catch (MapConflictAutoResolveException ex)
        {
            return ex.Message;
        }
        catch
        {
            return null;
            // Pull failed (e.g., uncommitted local changes) – continue with normal ls behavior.
        }
    }

    private CommandExecutionResult ProcessHide(string parameters)
    {
        return _map.HideNode(parameters)
            ? PersistMapChange("Hide node")
            : CommandExecutionResult.Error($"Can't find \"{parameters}\"");
    }

    private CommandExecutionResult ProcessLinkFrom(string parameters)
    {
        var nodeToLink = _map.GetNode(parameters);
        if (nodeToLink == null)
            return CommandExecutionResult.Error($"Couldn't find node \"{parameters}\"");

        _appContext.LinkIndex.QueueLinkSource(nodeToLink);
        return CommandExecutionResult.Success();
    }

    private CommandExecutionResult ProcessLinkTo(string parameters)
    {
        if (!_appContext.LinkIndex.HasQueuedLinkSources)
            return CommandExecutionResult.Error("No source node selected. Use linkfrom first");

        if (!_map.HasNode(parameters))
            return CommandExecutionResult.Error($"Couldn't find node \"{parameters}\"");

        var targetNodePeek = _map.GetNodeContentPeekByIdentifier(parameters);
        var nodeToLinkFrom = _appContext.LinkIndex.PeekQueuedLinkSource();
        var selectedRelationType = SelectLinkRelationType();
        if (!selectedRelationType.HasValue)
            return CommandExecutionResult.Error("Cancelled!");

        if (!new Confirmation(
                $"Link \"{NodeDisplayHelper.GetSingleLinePreview(nodeToLinkFrom.Name)}\" to \"{targetNodePeek}\" as \"{selectedRelationType.Value.ToDisplayString()}\"?")
            .Confirmed())
        {
            return CommandExecutionResult.Error("Cancelled!");
        }

        var linkResult = _map.LinkToNode(parameters, nodeToLinkFrom, selectedRelationType.Value, "Link Metadata");
        if (!linkResult)
            return CommandExecutionResult.Error($"Unexpected result while linking \"{parameters}\"");

        _appContext.LinkIndex.PopQueuedLinkSource();
        return PersistMapChange("Link nodes");
    }

    private CommandExecutionResult ProcessOpenLink()
    {
        var links = _appContext.LinkNavigationService.GetOutgoingLinks(_map.GetCurrentNode());
        if (!links.Any())
            return CommandExecutionResult.Error("Current node has no links");

        var sourceText = _map.GetCurrentNodeContentPeek();
        return NavigateToLinkedNode(links, $"Linked nodes — \"{sourceText}\"");
    }

    private CommandExecutionResult ProcessSearch(string parameters)
    {
        var searchResult = SearchCommandHelper.Run(
            parameters,
            query => MindMapSearchService.Search(_map, query, _filePath),
            includeMapName: false);

        if (searchResult.HasError)
            return CommandExecutionResult.Error(searchResult.ErrorMessage);

        if (searchResult.SelectedResult == null)
            return CommandExecutionResult.Success();

        return _map.ChangeCurrentNodeById(searchResult.SelectedResult.NodeId)
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.Error("Couldn't open selected result");
    }

    private CommandExecutionResult ProcessSetTaskState(string parameters, TaskState taskState)
    {
        string errorMessage;
        var success = string.IsNullOrWhiteSpace(parameters)
            ? _map.SetTaskState(taskState, out errorMessage)
            : _map.SetTaskState(parameters, taskState, out errorMessage);

        return success
            ? PersistMapChange(taskState switch
            {
                TaskState.Todo => "Mark task as todo",
                TaskState.Doing => "Mark task as doing",
                TaskState.Done => "Mark task as done",
                TaskState.None => "Clear task state",
                _ => "Update task state"
            })
            : CommandExecutionResult.Error(errorMessage);
    }

    private CommandExecutionResult ProcessSlice(string parameters)
    {
        var selectionMenu = new SelectionMenu(new List<string> { InOption, OutOption });
        selectionMenu.Show();
        return selectionMenu.GetSelectedOption() switch
        {
            1 => ProcessAttach(),
            2 => ProcessDetach(parameters),
            _ => CommandExecutionResult.Error("Operation Canceled")
        };
    }

    private CommandExecutionResult ProcessUnhide(string parameters)
    {
        return _map.UnhideNode(parameters)
            ? PersistMapChange("Unhide node")
            : CommandExecutionResult.Error($"Can't find \"{parameters}\"");
    }

    private CommandExecutionResult ProcessMeta(string parameters)
    {
        if (!TryResolveNodeForMetadataCommand(parameters, out var node, out var errorMessage))
            return CommandExecutionResult.Error(errorMessage!);

        var resolvedNode = node!;
        new NodeMetadataPage(resolvedNode, $"Metadata for {NodeDisplayHelper.GetContentPeek(resolvedNode.Name)}").Show();
        return CommandExecutionResult.Success();
    }

    private CommandExecutionResult ProcessTasks(string parameters)
    {
        if (!TaskCommandHelper.TryParseFilter(parameters, out var filter, out var errorMessage))
            return CommandExecutionResult.Error(errorMessage ?? "Unsupported task filter");

        var tasks = TaskQueryService.GetTasks(_map, _filePath, filter);
        if (!tasks.Any())
            return CommandExecutionResult.Error(TaskCommandHelper.BuildEmptyTasksMessage(filter, acrossAllMaps: false));

        var selectedResult = new SearchResultsPage(
            tasks,
            TaskCommandHelper.GetTasksTitle(filter, acrossAllMaps: false),
            new SearchResultDisplayOptions(
                includeMapName: false,
                colorizeAncestorPath: true,
                highlightTerms: Array.Empty<string>()))
            .SelectResult();

        if (selectedResult == null)
            return CommandExecutionResult.Success();

        return _map.ChangeCurrentNodeById(selectedResult.NodeId)
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.Error("Couldn't open selected task");
    }

    private CommandExecutionResult ProcessToggleTaskState(string parameters)
    {
        string errorMessage;
        var success = string.IsNullOrWhiteSpace(parameters)
            ? _map.ToggleTaskState(out errorMessage)
            : _map.ToggleTaskState(parameters, out errorMessage);

        return success
            ? PersistMapChange("Toggle task state")
            : CommandExecutionResult.Error(errorMessage);
    }

    private CommandExecutionResult ProcessSetHideDoneTasks(string parameters, bool hideDoneTasks)
    {
        string errorMessage;
        var success = string.IsNullOrWhiteSpace(parameters)
            ? _map.SetHideDoneTasks(hideDoneTasks, out errorMessage)
            : _map.SetHideDoneTasks(parameters, hideDoneTasks, out errorMessage);

        return success
            ? PersistMapChange(hideDoneTasks ? "Hide done tasks" : "Show done tasks")
            : CommandExecutionResult.Error(errorMessage);
    }

    private string BuildExportSyncCommitMessage(ExportFormat format) =>
        format == ExportFormat.Html
            ? BuildMapCommitMessage("Export HTML", "from")
            : BuildMapCommitMessage("Export markdown", "from");

    private bool TryResolveNodeForMetadataCommand(string parameters, out Node? node, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            node = _map.GetCurrentNode();
            errorMessage = null;
            return true;
        }

        node = _map.GetNode(parameters);
        errorMessage = node == null
            ? $"Can't find \"{parameters}\""
            : null;
        return node != null;
    }

    private string BuildMapCommitMessage(string action, string relation = "in")
    {
        var mapName = Path.GetFileNameWithoutExtension(_filePath);
        if (string.IsNullOrWhiteSpace(mapName))
            mapName = Path.GetFileName(_filePath);

        return $"{action} {relation} {mapName ?? "map"}";
    }

    private CommandExecutionResult PersistMapChange(string action, string relation = "in", string? message = null) =>
        CommandExecutionResult.SuccessAndPersist(message, BuildMapCommitMessage(action, relation));

    private IReadOnlyList<NodeAttachment> GetCurrentAttachments() =>
        _map.GetCurrentNode().Metadata?.Attachments ?? new List<NodeAttachment>();

    private IEnumerable<string> GetCurrentAttachmentSelectors()
    {
        var attachments = GetCurrentAttachments();
        for (var index = 1; index <= attachments.Count; index++)
        {
            yield return index.ToString();

            var shortcut = AccessibleKeyNumbering.GetStringFor(index);
            if (!string.IsNullOrWhiteSpace(shortcut))
                yield return shortcut;
        }
    }

    private string BuildCurrentAttachmentSummary()
    {
        var attachments = GetCurrentAttachments();
        if (attachments.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        builder.Append(":Attachments> ");
        for (var index = 0; index < attachments.Count; index++)
        {
            if (index > 0)
                builder.Append("; ");

            builder.Append(BuildAttachmentAddressMarkup(index + 1));
            builder.Append(' ');
            builder.Append(attachments[index].DisplayName.GetContentPeek());
        }

        builder.AppendLine();
        builder.AppendLine();
        return builder.ToString();
    }

    private bool TryOpenCurrentAttachmentShortcut(string command, out CommandExecutionResult result)
    {
        result = CommandExecutionResult.Success();
        var attachments = GetCurrentAttachments();
        if (attachments.Count == 0 || string.IsNullOrWhiteSpace(command))
            return false;

        if (!TryGetAttachmentIndex(command, out var attachmentIndex))
            return false;

        if (attachmentIndex > attachments.Count)
        {
            result = CommandExecutionResult.Error($"Can't find attachment \"{command}\"");
            return true;
        }

        result = OpenAttachment(attachments[attachmentIndex - 1]);
        return true;
    }

    private bool TryResolveCurrentAttachment(string parameters, out NodeAttachment? attachment, out string? errorMessage)
    {
        attachment = null;
        var attachments = GetCurrentAttachments();
        if (attachments.Count == 0)
        {
            errorMessage = "Current node has no attachments";
            return false;
        }

        var normalizedParameters = parameters.Trim().ToCommandKey();
        if (!TryGetAttachmentIndex(normalizedParameters, out var attachmentIndex))
        {
            errorMessage = $"Unknown attachment \"{parameters}\". Use a number or shortcut like \"{BuildAttachmentAddress(1)}\".";
            return false;
        }

        if (attachmentIndex > attachments.Count)
        {
            errorMessage = $"Can't find attachment \"{parameters}\"";
            return false;
        }

        attachment = attachments[attachmentIndex - 1];
        errorMessage = null;
        return true;
    }

    private static bool TryGetAttachmentIndex(string value, out int attachmentIndex)
    {
        if (int.TryParse(value, out attachmentIndex) && attachmentIndex > 0)
            return true;

        attachmentIndex = AccessibleKeyNumbering.GetNumberFor(value);
        return attachmentIndex > 0;
    }

    private CommandExecutionResult OpenAttachment(NodeAttachment attachment)
    {
        var currentNodeIdentifier = GetRequiredNodeIdentifier(_map.GetCurrentNode());
        var attachmentPath = _appContext.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            _filePath,
            currentNodeIdentifier,
            attachment.RelativePath);
        if (!File.Exists(attachmentPath))
            return CommandExecutionResult.Error($"Attachment \"{attachment.DisplayName}\" is missing");

        return _appContext.FileOpener.TryOpen(attachmentPath, out var openErrorMessage)
            ? CommandExecutionResult.Success($"Opened attachment \"{attachment.DisplayName}\"")
            : CommandExecutionResult.Error(openErrorMessage ?? "The attachment could not be opened.");
    }

    private static string BuildClipboardImageAttachmentDisplayName(DateTimeOffset timestampUtc) =>
        $"Screenshot {timestampUtc:yyyy-MM-dd HH:mm}.png";

    private static Guid GetRequiredNodeIdentifier(Node node) =>
        node.UniqueIdentifier ?? throw new InvalidOperationException("Node identifier is required for attachment operations.");

    private static string BuildClipboardTextAttachmentDisplayName(string clipboardText, DateTimeOffset timestampUtc) =>
        $"Clipboard text {timestampUtc:yyyy-MM-dd HH:mm} - {BuildClipboardTextPreview(clipboardText)}";

    private static string BuildAttachmentAddress(int index)
    {
        var shortcut = AccessibleKeyNumbering.GetStringFor(index);
        return string.IsNullOrWhiteSpace(shortcut)
            ? index.ToString()
            : $"{shortcut}/{index}";
    }

    private static string BuildAttachmentAddressMarkup(int index)
    {
        var shortcut = AccessibleKeyNumbering.GetStringFor(index);
        return string.IsNullOrWhiteSpace(shortcut)
            ? $"[{ConfigurationConstants.CommandColor}]{index}[!]"
            : $"[{ConfigurationConstants.CommandColor}]{shortcut}[!]/[{ConfigurationConstants.CommandColor}]{index}[!]";
    }

    private static string BuildClipboardTextPreview(string clipboardText)
    {
        var normalizedText = NormalizeWhitespace(clipboardText);
        if (string.IsNullOrWhiteSpace(normalizedText))
            return "Clipboard text...";

        if (normalizedText.Length <= ClipboardTextPreviewMaxLength)
            return normalizedText.EndsWith("...", StringComparison.Ordinal)
                ? normalizedText
                : $"{normalizedText}...";

        var searchLength = Math.Min(ClipboardTextPreviewMaxLength, normalizedText.Length);
        var preferredBreakIndex = normalizedText.LastIndexOf(' ', searchLength - 1, searchLength);
        if (preferredBreakIndex >= ClipboardTextPreviewMinLength)
            return $"{normalizedText[..preferredBreakIndex].TrimEnd()}...";

        return $"{normalizedText[..ClipboardTextPreviewMaxLength].TrimEnd()}...";
    }

    private static string NormalizeWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (builder.Length == 0 || previousWasWhitespace)
                    continue;

                builder.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    private static bool InvokeLocalized(Func<string, bool> action, string parameters)
    {
        return action(parameters)
               || action(parameters.ToCommandKey());
    }

    private CommandExecutionResult NavigateToLinkedNode(IReadOnlyList<NodeSearchResult> relatedNodes, string title)
    {
        var selectedResult = new SearchResultsPage(
            relatedNodes,
            title,
            new SearchResultDisplayOptions(
                includeMapName: true,
                colorizeAncestorPath: false,
                highlightTerms: Array.Empty<string>()))
            .SelectResult();
        if (selectedResult == null)
            return CommandExecutionResult.Success();

        if (string.Equals(selectedResult.MapFilePath, _filePath, StringComparison.InvariantCultureIgnoreCase))
        {
            return _map.ChangeCurrentNodeById(selectedResult.NodeId)
                ? CommandExecutionResult.Success()
                : CommandExecutionResult.Error("Couldn't open selected node");
        }

        try
        {
            _appContext.Navigator.OpenEditMap(selectedResult.MapFilePath, selectedResult.NodeId);
            return CommandExecutionResult.Success();
        }
        catch (MapConflictAutoResolveException ex)
        {
            return CommandExecutionResult.Error(ex.Message);
        }
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
