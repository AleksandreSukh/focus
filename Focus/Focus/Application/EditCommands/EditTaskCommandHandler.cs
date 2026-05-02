#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditTaskCommandHandler : IEditCommandFeatureHandler
{
    public IReadOnlyCollection<EditCommandId> CommandIds { get; } = new[]
    {
        EditCommandId.SetTaskTodo,
        EditCommandId.SetTaskDoing,
        EditCommandId.SetTaskDone,
        EditCommandId.ClearTaskState,
        EditCommandId.ToggleTaskState,
        EditCommandId.HideDoneTasks,
        EditCommandId.ShowDoneTasks,
        EditCommandId.ListTasks
    };

    public CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters) =>
        commandId switch
        {
            EditCommandId.SetTaskTodo => ProcessSetTaskState(context, parameters, TaskState.Todo),
            EditCommandId.SetTaskDoing => ProcessSetTaskState(context, parameters, TaskState.Doing),
            EditCommandId.SetTaskDone => ProcessSetTaskState(context, parameters, TaskState.Done),
            EditCommandId.ClearTaskState => ProcessSetTaskState(context, parameters, TaskState.None),
            EditCommandId.ToggleTaskState => ProcessToggleTaskState(context, parameters),
            EditCommandId.HideDoneTasks => ProcessSetHideDoneTasks(context, parameters, hideDoneTasks: true),
            EditCommandId.ShowDoneTasks => ProcessSetHideDoneTasks(context, parameters, hideDoneTasks: false),
            EditCommandId.ListTasks => ProcessTasks(context, parameters),
            _ => CommandExecutionResult.Error($"Unsupported command \"{commandId}\"")
        };

    private static CommandExecutionResult ProcessSetTaskState(
        EditCommandContext context,
        string parameters,
        TaskState taskState)
    {
        string errorMessage;
        var success = string.IsNullOrWhiteSpace(parameters)
            ? context.Map.SetTaskState(taskState, out errorMessage)
            : context.Map.SetTaskState(parameters, taskState, out errorMessage);

        return success
            ? context.PersistMapChange(taskState switch
            {
                TaskState.Todo => "Mark task as todo",
                TaskState.Doing => "Mark task as doing",
                TaskState.Done => "Mark task as done",
                TaskState.None => "Clear task state",
                _ => "Update task state"
            })
            : CommandExecutionResult.Error(errorMessage);
    }

    private static CommandExecutionResult ProcessToggleTaskState(EditCommandContext context, string parameters)
    {
        string errorMessage;
        var success = string.IsNullOrWhiteSpace(parameters)
            ? context.Map.ToggleTaskState(out errorMessage)
            : context.Map.ToggleTaskState(parameters, out errorMessage);

        return success
            ? context.PersistMapChange("Toggle task state")
            : CommandExecutionResult.Error(errorMessage);
    }

    private static CommandExecutionResult ProcessSetHideDoneTasks(
        EditCommandContext context,
        string parameters,
        bool hideDoneTasks)
    {
        string errorMessage;
        var success = string.IsNullOrWhiteSpace(parameters)
            ? context.Map.SetHideDoneTasks(hideDoneTasks, out errorMessage)
            : context.Map.SetHideDoneTasks(parameters, hideDoneTasks, out errorMessage);

        return success
            ? context.PersistMapChange(hideDoneTasks ? "Hide done tasks" : "Show done tasks")
            : CommandExecutionResult.Error(errorMessage);
    }

    private static CommandExecutionResult ProcessTasks(EditCommandContext context, string parameters)
    {
        if (!TaskCommandHelper.TryParseFilter(parameters, out var filter, out var errorMessage))
            return CommandExecutionResult.Error(errorMessage ?? "Unsupported task filter");

        var tasks = TaskQueryService.GetTasks(context.Map, context.FilePath, filter);
        if (!tasks.Any())
            return CommandExecutionResult.Error(TaskCommandHelper.BuildEmptyTasksMessage(filter, acrossAllMaps: false));

        var selectedResult = context.AppContext.WorkflowInteractions.SelectSearchResult(
            tasks,
            TaskCommandHelper.GetTasksTitle(filter, acrossAllMaps: false),
            new SearchResultDisplayOptions(
                includeMapName: false,
                colorizeAncestorPath: true,
                highlightTerms: Array.Empty<string>()));

        if (selectedResult == null)
            return CommandExecutionResult.Success();

        return context.Map.ChangeCurrentNodeById(selectedResult.NodeId)
            ? CommandExecutionResult.Success()
            : CommandExecutionResult.Error("Couldn't open selected task");
    }
}
