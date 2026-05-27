#nullable enable

using System;
using System.IO;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Application.Llm;

internal sealed class LlmJobProcessor
{
    private readonly FocusAppContext _appContext;
    private readonly LlmJobStore _jobStore;
    private readonly LlmContextBuilder _contextBuilder = new();

    public LlmJobProcessor(FocusAppContext appContext)
    {
        _appContext = appContext;
        _jobStore = new LlmJobStore(appContext.MapsStorage.UserMindMapsDirectory);
    }

    public LlmJobProcessResult Process(
        LlmJobEntry jobEntry,
        MindMap map,
        string mapFilePath,
        bool mapAlreadyChanged)
    {
        var claimedEntry = _jobStore.Claim(jobEntry, LlmPromptService.DefaultAgentName);
        var context = _contextBuilder.Build(_appContext, map, mapFilePath, claimedEntry.Job.NodeId);
        if (context == null)
        {
            _jobStore.Fail(claimedEntry, $"Prompt node \"{claimedEntry.Job.NodeId}\" was not found.");
            return LlmJobProcessResult.Failed("AI prompt node was not found.", mapAlreadyChanged);
        }

        var contextJson = _contextBuilder.ToJson(context);
        var contextMarkdown = _contextBuilder.ToMarkdown(context);
        LlmAgentResponse agentResult;
        using (_appContext.StatusSink.ShowBusy("Waiting for Codex..."))
        {
            agentResult = _appContext.LlmAgentClient.Run(new LlmAgentRequest(
                LlmPromptService.DefaultAgentName,
                context.Prompt.Text,
                contextMarkdown,
                contextJson,
                _appContext.MapsStorage.UserMindMapsDirectory));
        }

        if (!agentResult.IsSuccess || string.IsNullOrWhiteSpace(agentResult.Answer))
        {
            var errorMessage = agentResult.ErrorMessage ?? "Codex returned an empty answer.";
            _jobStore.Fail(claimedEntry, errorMessage);
            return LlmJobProcessResult.Failed($"AI request failed: {errorMessage}", mapAlreadyChanged);
        }

        var answerNodeId = ApplyAnswer(
            map,
            claimedEntry.Job.NodeId,
            agentResult.Answer,
            LlmPromptService.DefaultAgentName);
        if (!answerNodeId.HasValue)
        {
            _jobStore.Fail(claimedEntry, $"Prompt node \"{claimedEntry.Job.NodeId}\" was not found.");
            return LlmJobProcessResult.Failed("AI prompt node was not found.", mapAlreadyChanged);
        }

        _jobStore.Complete(claimedEntry, answerNodeId.Value, LlmPromptService.DefaultAgentName);
        return LlmJobProcessResult.Completed(
            $"AI answer appended to \"{Path.GetFileNameWithoutExtension(mapFilePath)}\".",
            mapChanged: true);
    }

    private static Guid? ApplyAnswer(MindMap map, Guid promptNodeId, string answer, string agentName)
    {
        var promptNode = FindNodeById(map.RootNode, promptNodeId);
        if (promptNode == null)
            return null;

        var answerNode = promptNode.Add(
            answer.Trim(),
            NodeType.TextBlockItem,
            $"llm:{agentName}",
            agentName);
        answerNode.TaskState = TaskState.None;
        promptNode.TaskState = TaskState.Done;
        promptNode.TouchMetadata();
        map.UpdatedAt = DateTimeOffset.UtcNow;
        return answerNode.UniqueIdentifier;
    }

    internal static Node? FindNodeById(Node node, Guid nodeId)
    {
        if (node.UniqueIdentifier == nodeId)
            return node;

        foreach (var child in node.Children)
        {
            var match = FindNodeById(child, nodeId);
            if (match != null)
                return match;
        }

        return null;
    }
}

internal sealed record LlmJobProcessResult(bool Succeeded, string Message, bool MapChanged)
{
    public static LlmJobProcessResult Completed(string message, bool mapChanged) =>
        new(true, message, mapChanged);

    public static LlmJobProcessResult Failed(string message, bool mapChanged) =>
        new(false, message, mapChanged);
}
