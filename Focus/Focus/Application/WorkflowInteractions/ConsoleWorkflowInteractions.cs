#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Application.WorkflowInteractions;

internal sealed class ConsoleWorkflowInteractions : IWorkflowInteractions
{
    public bool Confirm(string message) =>
        new Confirmation(message).Confirmed();

    public bool AddNotes(MindMap map, string? initialInput = null)
    {
        var dialog = new AddNoteDialog(map);
        if (string.IsNullOrWhiteSpace(initialInput))
        {
            dialog.Show();
        }
        else
        {
            dialog.ShowWithInitialInput(initialInput);
        }

        return dialog.DidAddNodes;
    }

    public bool AddBlock(MindMap map)
    {
        var dialog = new AddBlockDialog(map);
        dialog.Show();
        return dialog.DidAddBlock;
    }

    public bool AddIdeas(MindMap map, string? initialInput = null)
    {
        var dialog = new AddIdeaDialog(map);
        if (string.IsNullOrWhiteSpace(initialInput))
        {
            dialog.Show();
        }
        else
        {
            dialog.ShowWithInitialInput(initialInput);
        }

        return dialog.DidAddIdeas;
    }

    public bool EditTextNode(MindMap map)
    {
        var dialog = new EditDialog(map, parameters: string.Empty);
        dialog.Show();
        return dialog.DidEdit;
    }

    public bool EditBlockNode(MindMap map)
    {
        var dialog = new EditBlockDialog(map);
        dialog.Show();
        return dialog.DidEdit;
    }

    public bool AttachMapIntoCurrentNode(MindMap map, FocusAppContext appContext, string targetMapFilePath)
    {
        var attachMode = new AttachMode(map, appContext, targetMapFilePath);
        attachMode.Show();
        return attachMode.DidAttachMap;
    }

    public string? RenameMapFile(IMapRepository mapRepository, FileInfo existingFile)
    {
        var dialog = new RenameFileDialog(mapRepository, existingFile);
        dialog.Show();
        return dialog.NewFilePath;
    }

    public string? RequestAvailableFilePath(string directoryPath, string fileName, string fileExtension)
    {
        string? selectedFilePath = null;
        new RequestRenameUntilFileNameIsAvailableDialog(
                directoryPath,
                fileName,
                filePath => selectedFilePath = filePath,
                fileExtension)
            .Show();
        return selectedFilePath;
    }

    public ExportRequest? SelectExport(string defaultFileName)
    {
        var exportPage = new ExportPage(defaultFileName);
        exportPage.Show();
        return exportPage.SelectedExport;
    }

    public NodeSearchResult? SelectSearchResult(
        IReadOnlyList<NodeSearchResult> results,
        string title,
        SearchResultDisplayOptions options) =>
        new SearchResultsPage(results, title, options).SelectResult();

    public int SelectOption(IReadOnlyList<string> options)
    {
        var selectionMenu = new SelectionMenu(options);
        selectionMenu.Show();
        return selectionMenu.GetSelectedOption();
    }

    public void ShowNodeMetadata(Node node, string title) =>
        new NodeMetadataPage(node, title).Show();

    public WorkflowVoiceRecordingDecision WaitForVoiceRecordingDecision(
        DateTimeOffset startedAtUtc,
        TimeSpan maxDuration)
    {
        var lastDisplayedSecond = -1;
        while (true)
        {
            var elapsed = DateTimeOffset.UtcNow - startedAtUtc;
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            if (elapsed >= maxDuration)
            {
                WriteVoiceRecordingStatus(maxDuration, reachedLimit: true);
                return WorkflowVoiceRecordingDecision.TimeLimitReached;
            }

            var elapsedSecond = (int)elapsed.TotalSeconds;
            if (elapsedSecond != lastDisplayedSecond)
            {
                WriteVoiceRecordingStatus(elapsed, reachedLimit: false);
                lastDisplayedSecond = elapsedSecond;
            }

            if (AppConsole.Current.KeyAvailable)
            {
                var key = AppConsole.Current.ReadKey(intercept: true);
                if (key.Key is ConsoleKey.Enter)
                {
                    AppConsole.Current.WriteLine(string.Empty);
                    return WorkflowVoiceRecordingDecision.Submit;
                }

                if (key.Key is ConsoleKey.Escape)
                {
                    AppConsole.Current.WriteLine(string.Empty);
                    return WorkflowVoiceRecordingDecision.Cancel;
                }
            }

            Thread.Sleep(100);
        }
    }

    private static void WriteVoiceRecordingStatus(TimeSpan elapsed, bool reachedLimit)
    {
        var elapsedText = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
        var suffix = reachedLimit
            ? "5 minute limit reached. Saving..."
            : "Press Enter to save or Esc to cancel.";
        AppConsole.Current.Write($"\rRecording voice note {elapsedText}. {suffix}   ");
    }
}
