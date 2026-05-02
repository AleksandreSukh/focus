#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.Application.WorkflowInteractions;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Tests;

internal sealed class RecordingWorkflowInteractions : IWorkflowInteractions
{
    private readonly Queue<bool> _confirmationResults = new();
    private readonly Queue<int> _optionSelections = new();
    private readonly Queue<WorkflowVoiceRecordingDecision> _voiceRecordingDecisions = new();

    public bool DefaultConfirmationResult { get; set; } = true;

    public bool AddNotesResult { get; set; }

    public bool AddBlockResult { get; set; }

    public bool AddIdeasResult { get; set; }

    public bool EditTextNodeResult { get; set; }

    public bool EditBlockNodeResult { get; set; }

    public bool AttachMapResult { get; set; }

    public string? RenameMapFilePath { get; set; }

    public string? AvailableFilePath { get; set; }

    public bool CancelAvailableFilePathRequest { get; set; }

    public ExportRequest? ExportRequest { get; set; }

    public Func<IReadOnlyList<NodeSearchResult>, NodeSearchResult?>? SearchResultSelector { get; set; }

    public Func<IMapRepository, FileInfo, string?>? RenameMapFileSelector { get; set; }

    public Action<MindMap, string?>? AddNotesAction { get; set; }

    public Action<MindMap>? AddBlockAction { get; set; }

    public Action<MindMap, string?>? AddIdeasAction { get; set; }

    public Action<MindMap>? EditTextNodeAction { get; set; }

    public Action<MindMap>? EditBlockNodeAction { get; set; }

    public List<string> ConfirmationMessages { get; } = new();

    public List<string?> AddNotesInitialInputs { get; } = new();

    public List<string?> AddIdeasInitialInputs { get; } = new();

    public List<string> SearchSelectionTitles { get; } = new();

    public List<SearchResultDisplayOptions> SearchSelectionDisplayOptions { get; } = new();

    public List<IReadOnlyList<NodeSearchResult>> SearchSelectionResults { get; } = new();

    public List<IReadOnlyList<string>> OptionSelectionOptions { get; } = new();

    public List<string> NodeMetadataTitles { get; } = new();

    public List<string> SelectExportDefaultFileNames { get; } = new();

    public List<(string DirectoryPath, string FileName, string FileExtension)> RequestedAvailableFilePaths { get; } = new();

    public int AddBlockCallCount { get; private set; }

    public int EditTextNodeCallCount { get; private set; }

    public int EditBlockNodeCallCount { get; private set; }

    public int AttachMapCallCount { get; private set; }

    public int RenameMapFileCallCount { get; private set; }

    public int VoiceRecordingDecisionCallCount { get; private set; }

    public void EnqueueConfirmation(bool result) => _confirmationResults.Enqueue(result);

    public void EnqueueOptionSelection(int option) => _optionSelections.Enqueue(option);

    public void EnqueueVoiceRecordingDecision(WorkflowVoiceRecordingDecision decision) =>
        _voiceRecordingDecisions.Enqueue(decision);

    public bool Confirm(string message)
    {
        ConfirmationMessages.Add(message);
        return _confirmationResults.Count > 0
            ? _confirmationResults.Dequeue()
            : DefaultConfirmationResult;
    }

    public bool AddNotes(MindMap map, string? initialInput = null)
    {
        AddNotesInitialInputs.Add(initialInput);
        AddNotesAction?.Invoke(map, initialInput);
        return AddNotesResult;
    }

    public bool AddBlock(MindMap map)
    {
        AddBlockCallCount++;
        AddBlockAction?.Invoke(map);
        return AddBlockResult;
    }

    public bool AddIdeas(MindMap map, string? initialInput = null)
    {
        AddIdeasInitialInputs.Add(initialInput);
        AddIdeasAction?.Invoke(map, initialInput);
        return AddIdeasResult;
    }

    public bool EditTextNode(MindMap map)
    {
        EditTextNodeCallCount++;
        EditTextNodeAction?.Invoke(map);
        return EditTextNodeResult;
    }

    public bool EditBlockNode(MindMap map)
    {
        EditBlockNodeCallCount++;
        EditBlockNodeAction?.Invoke(map);
        return EditBlockNodeResult;
    }

    public bool AttachMapIntoCurrentNode(MindMap map, FocusAppContext appContext, string targetMapFilePath)
    {
        AttachMapCallCount++;
        return AttachMapResult;
    }

    public string? RenameMapFile(IMapRepository mapRepository, FileInfo existingFile)
    {
        RenameMapFileCallCount++;
        return RenameMapFileSelector?.Invoke(mapRepository, existingFile) ?? RenameMapFilePath;
    }

    public string? RequestAvailableFilePath(string directoryPath, string fileName, string fileExtension)
    {
        RequestedAvailableFilePaths.Add((directoryPath, fileName, fileExtension));
        if (CancelAvailableFilePathRequest)
            return null;

        return AvailableFilePath ?? MapFilePathHelper.GetFullFilePath(directoryPath, fileName, fileExtension);
    }

    public ExportRequest? SelectExport(string defaultFileName)
    {
        SelectExportDefaultFileNames.Add(defaultFileName);
        return ExportRequest;
    }

    public NodeSearchResult? SelectSearchResult(
        IReadOnlyList<NodeSearchResult> results,
        string title,
        SearchResultDisplayOptions options)
    {
        SearchSelectionResults.Add(results);
        SearchSelectionTitles.Add(title);
        SearchSelectionDisplayOptions.Add(options);
        return SearchResultSelector?.Invoke(results);
    }

    public int SelectOption(IReadOnlyList<string> options)
    {
        OptionSelectionOptions.Add(options);
        return _optionSelections.Count > 0
            ? _optionSelections.Dequeue()
            : 0;
    }

    public void ShowNodeMetadata(Node node, string title)
    {
        NodeMetadataTitles.Add(title);
    }

    public WorkflowVoiceRecordingDecision WaitForVoiceRecordingDecision(
        DateTimeOffset startedAtUtc,
        TimeSpan maxDuration)
    {
        VoiceRecordingDecisionCallCount++;
        return _voiceRecordingDecisions.Count > 0
            ? _voiceRecordingDecisions.Dequeue()
            : WorkflowVoiceRecordingDecision.Submit;
    }
}
