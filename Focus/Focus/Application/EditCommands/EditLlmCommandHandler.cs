#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Application.Llm;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditLlmCommandHandler : IEditCommandFeatureHandler
{
    public IReadOnlyCollection<EditCommandId> CommandIds { get; } = new[]
    {
        EditCommandId.Ai,
        EditCommandId.AiJobs
    };

    public CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters) =>
        commandId switch
        {
            EditCommandId.Ai => ProcessAi(context, parameters),
            EditCommandId.AiJobs => ProcessAiJobs(context, parameters),
            _ => CommandExecutionResult.Error($"Unsupported command \"{commandId}\"")
        };

    private static CommandExecutionResult ProcessAi(EditCommandContext context, string parameters)
    {
        var jobStore = new LlmJobStore(context.AppContext.MapsStorage.UserMindMapsDirectory);
        var processor = new LlmJobProcessor(context.AppContext);

        if (string.IsNullOrWhiteSpace(parameters))
        {
            var currentNode = context.Map.GetCurrentNode();
            if (!LlmPromptService.IsPromptNode(currentNode))
            {
                return CommandExecutionResult.Error(
                    "Current node is not an open @ai prompt. Use ai <prompt>.");
            }

            return ProcessNodePrompt(context, jobStore, processor, currentNode);
        }

        var targetNode = context.Map.GetNode(parameters);
        if (targetNode != null)
        {
            return ProcessNodePrompt(context, jobStore, processor, targetNode);
        }

        return ProcessNewPrompt(context, jobStore, processor, parameters);
    }

    private static CommandExecutionResult ProcessNewPrompt(
        EditCommandContext context,
        LlmJobStore jobStore,
        LlmJobProcessor processor,
        string prompt)
    {
        var promptText = LlmPromptService.ExtractPromptText(prompt);
        if (string.IsNullOrWhiteSpace(promptText))
            promptText = prompt.Trim();
        if (string.IsNullOrWhiteSpace(promptText))
            return CommandExecutionResult.Error("AI prompt is empty.");

        var promptNode = context.Map.AddAtCurrentNode(LlmPromptService.BuildPromptNodeName(promptText));
        promptNode.TaskState = TaskState.Todo;
        promptNode.TouchMetadata();
        var promptNodeId = promptNode.UniqueIdentifier
            ?? throw new InvalidOperationException("AI prompt node has no identifier.");

        var jobEntry = jobStore.CreateJob(context.FilePath, promptNodeId, promptText);
        var result = processor.Process(jobEntry, context.Map, context.FilePath, mapAlreadyChanged: true);
        return result.Succeeded
            ? context.PersistMapChange("Answer AI prompt", message: result.Message)
            : context.PersistMapChange("Create AI prompt", message: result.Message);
    }

    private static CommandExecutionResult ProcessNodePrompt(
        EditCommandContext context,
        LlmJobStore jobStore,
        LlmJobProcessor processor,
        Node promptNode)
    {
        var promptNodeId = promptNode.UniqueIdentifier
            ?? throw new InvalidOperationException("AI prompt node has no identifier.");
        var promptText = LlmPromptService.GetPromptText(promptNode);
        if (string.IsNullOrWhiteSpace(promptText))
            return CommandExecutionResult.Error("AI prompt is empty.");

        var existingEntry = jobStore.FindOpenByNode(context.FilePath, promptNodeId);
        if (existingEntry?.Job.Status == LlmJobStatus.Claimed)
        {
            return CommandExecutionResult.Error(
                $"AI job \"{existingEntry.Job.Id}\" is already claimed by {existingEntry.Job.ClaimedBy ?? "another agent"}.");
        }

        var jobEntry = existingEntry ?? jobStore.CreateJob(
            context.FilePath,
            promptNodeId,
            promptText);
        var result = processor.Process(jobEntry, context.Map, context.FilePath, mapAlreadyChanged: false);
        if (result.Succeeded)
            return context.PersistMapChange("Answer AI prompt", message: result.Message);

        SyncJobChange(context, jobEntry.Job.Id, LlmJobStatus.Failed);
        return CommandExecutionResult.Error(result.Message);
    }

    private static CommandExecutionResult ProcessAiJobs(EditCommandContext context, string parameters)
    {
        var jobStore = new LlmJobStore(context.AppContext.MapsStorage.UserMindMapsDirectory);
        if (string.IsNullOrWhiteSpace(parameters))
            return ListOpenJobs(jobStore);

        var parts = parameters.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return ListOpenJobs(jobStore);

        if (!string.Equals(parts[0], "run", StringComparison.OrdinalIgnoreCase))
            return CommandExecutionResult.Error("Unsupported AI jobs command. Use aijobs or aijobs run [jobId].");

        var jobId = parts.Length > 1 ? parts[1] : null;
        return RunPendingJob(context, jobStore, jobId);
    }

    private static CommandExecutionResult ListOpenJobs(LlmJobStore jobStore)
    {
        var jobs = jobStore.ListJobs()
            .Where(entry => LlmJobStore.IsOpen(entry.Job.Status))
            .ToArray();
        if (jobs.Length == 0)
            return CommandExecutionResult.Success("No open AI jobs.");

        return CommandExecutionResult.Success(string.Join(
            Environment.NewLine,
            jobs.Select(entry =>
                $"{entry.Job.Status.PadRight(9)} {entry.Job.Id} {entry.Job.MapFilePath}#{entry.Job.NodeId} {entry.Job.Prompt}")));
    }

    private static CommandExecutionResult RunPendingJob(
        EditCommandContext context,
        LlmJobStore jobStore,
        string? jobId)
    {
        var jobEntry = jobStore.FindOldestPending(jobId);
        if (jobEntry == null)
        {
            return CommandExecutionResult.Error(
                string.IsNullOrWhiteSpace(jobId)
                    ? "No pending AI jobs found."
                    : $"Pending AI job \"{jobId}\" was not found.");
        }

        string mapFilePath;
        try
        {
            mapFilePath = jobStore.ResolveMapPath(jobEntry.Job.MapFilePath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            jobStore.Fail(jobEntry, ex.Message);
            SyncJobChange(context, jobEntry.Job.Id, LlmJobStatus.Failed);
            return CommandExecutionResult.Error(ex.Message);
        }

        var isCurrentMap = IsSamePath(mapFilePath, context.FilePath);
        var map = isCurrentMap
            ? context.Map
            : context.AppContext.MapRepository.OpenMapForEditing(mapFilePath);
        var result = new LlmJobProcessor(context.AppContext).Process(
            jobEntry,
            map,
            mapFilePath,
            mapAlreadyChanged: false);

        if (!result.Succeeded)
        {
            SyncJobChange(context, jobEntry.Job.Id, LlmJobStatus.Failed);
            return CommandExecutionResult.Error(result.Message);
        }

        if (isCurrentMap)
            return context.PersistMapChange("Answer AI prompt", message: result.Message);

        context.AppContext.MapRepository.SaveMap(mapFilePath, map);
        context.AppContext.RefreshLinkIndex();
        context.AppContext.MapsStorage.Sync(BuildMapCommitMessage("Answer AI prompt", mapFilePath));
        return CommandExecutionResult.Success(result.Message);
    }

    private static void SyncJobChange(EditCommandContext context, string jobId, string status) =>
        context.AppContext.MapsStorage.Sync($"llm:job {jobId} -> {status}");

    private static string BuildMapCommitMessage(string action, string filePath)
    {
        var mapName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(mapName))
            mapName = Path.GetFileName(filePath);

        return $"{action} in {mapName ?? "map"}";
    }

    private static bool IsSamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
}
