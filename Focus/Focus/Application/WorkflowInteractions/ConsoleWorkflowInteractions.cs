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
    private const int VoiceRecordingProgressBarWidth = 24;

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
        var lastStatusLength = 0;
        while (true)
        {
            var elapsed = DateTimeOffset.UtcNow - startedAtUtc;
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            if (elapsed >= maxDuration)
            {
                WriteVoiceRecordingStatus(maxDuration, maxDuration, reachedLimit: true, ref lastStatusLength);
                return WorkflowVoiceRecordingDecision.TimeLimitReached;
            }

            var elapsedSecond = (int)elapsed.TotalSeconds;
            if (elapsedSecond != lastDisplayedSecond)
            {
                WriteVoiceRecordingStatus(elapsed, maxDuration, reachedLimit: false, ref lastStatusLength);
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

    internal static string FormatVoiceRecordingStatus(TimeSpan elapsed, TimeSpan maxDuration, bool reachedLimit)
    {
        var boundedElapsed = ClampElapsed(elapsed, maxDuration);
        var progress = maxDuration > TimeSpan.Zero
            ? boundedElapsed.TotalMilliseconds / maxDuration.TotalMilliseconds
            : 1;
        progress = Math.Clamp(progress, 0, 1);

        var filledWidth = (int)Math.Round(
            progress * VoiceRecordingProgressBarWidth,
            MidpointRounding.AwayFromZero);
        filledWidth = Math.Clamp(filledWidth, 0, VoiceRecordingProgressBarWidth);
        var emptyWidth = VoiceRecordingProgressBarWidth - filledWidth;
        var elapsedText = FormatVoiceRecordingTime(boundedElapsed);
        var maxDurationText = FormatVoiceRecordingTime(maxDuration > TimeSpan.Zero
            ? maxDuration
            : TimeSpan.Zero);
        var percentage = (int)Math.Round(progress * 100, MidpointRounding.AwayFromZero);
        var suffix = reachedLimit
            ? "5 minute limit reached. Saving..."
            : "Press Enter to save or Esc to cancel.";

        return $"Recording voice note [{new string('#', filledWidth)}{new string('-', emptyWidth)}] {elapsedText} / {maxDurationText} {percentage}%. {suffix}";
    }

    private static void WriteVoiceRecordingStatus(
        TimeSpan elapsed,
        TimeSpan maxDuration,
        bool reachedLimit,
        ref int lastStatusLength)
    {
        var status = FormatVoiceRecordingStatus(elapsed, maxDuration, reachedLimit);
        var clearSuffix = new string(' ', Math.Max(0, lastStatusLength - status.Length));
        AppConsole.Current.Write($"\r{status}{clearSuffix}");
        lastStatusLength = status.Length;
    }

    private static TimeSpan ClampElapsed(TimeSpan elapsed, TimeSpan maxDuration)
    {
        if (elapsed < TimeSpan.Zero)
            return TimeSpan.Zero;

        if (maxDuration <= TimeSpan.Zero)
            return TimeSpan.Zero;

        return elapsed > maxDuration
            ? maxDuration
            : elapsed;
    }

    private static string FormatVoiceRecordingTime(TimeSpan value) =>
        $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
}
