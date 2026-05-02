#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Application.WorkflowInteractions;

internal interface IWorkflowInteractions
{
    bool Confirm(string message);

    bool AddNotes(MindMap map, string? initialInput = null);

    bool AddBlock(MindMap map);

    bool AddIdeas(MindMap map, string? initialInput = null);

    bool EditTextNode(MindMap map);

    bool EditBlockNode(MindMap map);

    bool AttachMapIntoCurrentNode(MindMap map, FocusAppContext appContext, string targetMapFilePath);

    string? RenameMapFile(IMapRepository mapRepository, FileInfo existingFile);

    string? RequestAvailableFilePath(string directoryPath, string fileName, string fileExtension);

    ExportRequest? SelectExport(string defaultFileName);

    NodeSearchResult? SelectSearchResult(
        IReadOnlyList<NodeSearchResult> results,
        string title,
        SearchResultDisplayOptions options);

    int SelectOption(IReadOnlyList<string> options);

    void ShowNodeMetadata(Node node, string title);

    WorkflowVoiceRecordingDecision WaitForVoiceRecordingDecision(
        DateTimeOffset startedAtUtc,
        TimeSpan maxDuration);
}
