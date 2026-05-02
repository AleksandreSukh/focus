#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditNodeCommandHandler : IEditCommandFeatureHandler
{
    private const string InOption = "in";
    private const string OutOption = "out";

    public IReadOnlyCollection<EditCommandId> CommandIds { get; } = new[]
    {
        EditCommandId.Add,
        EditCommandId.AddBlock,
        EditCommandId.AddIdea,
        EditCommandId.ClearIdeas,
        EditCommandId.Delete,
        EditCommandId.Edit,
        EditCommandId.Hide,
        EditCommandId.Slice,
        EditCommandId.Unhide
    };

    public CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters) =>
        commandId switch
        {
            EditCommandId.Add => ProcessAdd(context),
            EditCommandId.AddBlock => ProcessAddBlock(context),
            EditCommandId.AddIdea => ProcessAddIdea(context, parameters),
            EditCommandId.ClearIdeas => ProcessClearIdeas(context, parameters),
            EditCommandId.Delete => ProcessCommandDel(context, parameters),
            EditCommandId.Edit => ProcessEdit(context),
            EditCommandId.Hide => ProcessHide(context, parameters),
            EditCommandId.Slice => ProcessSlice(context, parameters),
            EditCommandId.Unhide => ProcessUnhide(context, parameters),
            _ => CommandExecutionResult.Error($"Unsupported command \"{commandId}\"")
        };

    private static CommandExecutionResult ProcessAdd(EditCommandContext context)
    {
        return context.AppContext.WorkflowInteractions.AddNotes(context.Map)
            ? context.PersistMapChange("Add note")
            : CommandExecutionResult.Success();
    }

    private static CommandExecutionResult ProcessAddBlock(EditCommandContext context)
    {
        return context.AppContext.WorkflowInteractions.AddBlock(context.Map)
            ? context.PersistMapChange("Add block")
            : CommandExecutionResult.Success();
    }

    private static CommandExecutionResult ProcessAddIdea(EditCommandContext context, string parameters)
    {
        return context.AppContext.WorkflowInteractions.AddIdeas(context.Map, parameters)
            ? context.PersistMapChange("Add idea")
            : CommandExecutionResult.Success();
    }

    private static CommandExecutionResult ProcessClearIdeas(EditCommandContext context, string parameters)
    {
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            if (!context.Map.HasNode(parameters))
                return CommandExecutionResult.Error($"Can't find \"{parameters}\"");

            var nodeContentPreview = context.Map.GetNodeContentPeekByIdentifier(parameters);
            if (!context.AppContext.WorkflowInteractions.Confirm(
                    $"Clear idea tags for: \"{parameters}\". \"{nodeContentPreview}\"?"))
                return CommandExecutionResult.Error("Cancelled!");

            return context.Map.DeleteNodeIdeaTags(parameters)
                ? context.PersistMapChange("Clear ideas")
                : CommandExecutionResult.Error($"Couldn't remove \"{parameters}\"");
        }

        if (!context.AppContext.WorkflowInteractions.Confirm("Clear idea tags for current node?"))
            return CommandExecutionResult.Error("Cancelled!");

        return context.Map.DeleteCurrentNodeIdeaTags()
            ? context.PersistMapChange("Clear ideas")
            : CommandExecutionResult.Error("Can't delete current node (report a bug)");
    }

    private static CommandExecutionResult ProcessCommandDel(EditCommandContext context, string parameters)
    {
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            if (!context.Map.HasNode(parameters))
                return CommandExecutionResult.Error($"Can't find \"{parameters}\"");

            var nodeContentPreview = context.Map.GetNodeContentPeekByIdentifier(parameters);
            if (!context.AppContext.WorkflowInteractions.Confirm(
                    $"Are you sure to delete \"{parameters}\". \"{nodeContentPreview}\""))
                return CommandExecutionResult.Error("Cancelled!");

            return context.Map.DeleteChildNode(parameters)
                ? context.PersistMapChange("Delete node")
                : CommandExecutionResult.Error($"Couldn't remove \"{parameters}\"");
        }

        var contentPeek = context.Map.GetCurrentNodeContentPeek();
        var nodeToDelete = context.Map.GetCurrentNodeName();
        if (context.Map.IsAtRootNode() || !context.Map.GoUp())
            return CommandExecutionResult.Error("Can't remove root node");

        if (!context.AppContext.WorkflowInteractions.Confirm($"Are you sure to delete current node:{contentPeek}"))
            return CommandExecutionResult.Error("Cancelled!");

        return context.Map.DeleteChildNode(nodeToDelete)
            ? context.PersistMapChange("Delete node")
            : CommandExecutionResult.Error("Can't delete current node (report a bug)");
    }

    private static CommandExecutionResult ProcessEdit(EditCommandContext context)
    {
        var didEdit = context.Map.GetCurrentNode().NodeType == NodeType.TextBlockItem
            ? context.AppContext.WorkflowInteractions.EditBlockNode(context.Map)
            : context.AppContext.WorkflowInteractions.EditTextNode(context.Map);

        if (!didEdit)
            return CommandExecutionResult.Success();

        if (context.Map.IsAtRootNode())
            RenameFileToMatchRootNode(context);

        return context.PersistMapChange("Edit node");
    }

    private static void RenameFileToMatchRootNode(EditCommandContext context)
    {
        var directory = Path.GetDirectoryName(context.FilePath);
        if (directory == null)
            return;

        var newBaseName = MapFilePathHelper.SanitizeFileName(context.Map.RootNode.Name);
        var newFilePath = MapFilePathHelper.GetFullFilePath(directory, newBaseName);

        if (string.Equals(context.FilePath, newFilePath, StringComparison.OrdinalIgnoreCase))
            return;

        var candidatePath = newFilePath;
        var counter = 2;
        while (File.Exists(candidatePath) && !string.Equals(candidatePath, context.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            var candidate = $"{newBaseName}_({counter})";
            candidatePath = MapFilePathHelper.GetFullFilePath(directory, candidate);
            counter++;
        }

        context.AppContext.MapRepository.MoveMap(context.FilePath, candidatePath);
        context.FilePath = candidatePath;
    }

    private static CommandExecutionResult ProcessSlice(EditCommandContext context, string parameters)
    {
        return context.AppContext.WorkflowInteractions.SelectOption(new List<string> { InOption, OutOption }) switch
        {
            1 => ProcessAttach(context),
            2 => ProcessDetach(context, parameters),
            _ => CommandExecutionResult.Error("Operation Canceled")
        };
    }

    private static CommandExecutionResult ProcessAttach(EditCommandContext context)
    {
        return context.AppContext.WorkflowInteractions.AttachMapIntoCurrentNode(context.Map, context.AppContext, context.FilePath)
            ? context.PersistMapChange("Attach map", relation: "into")
            : CommandExecutionResult.Success();
    }

    private static CommandExecutionResult ProcessDetach(EditCommandContext context, string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            if (!context.AppContext.WorkflowInteractions.Confirm($"Detach current node? {context.Map.GetCurrentNodeContentPeek()}"))
                return CommandExecutionResult.Error("Cancelled");

            var detachedMap = context.Map.DetachCurrentNodeAsNewMap();
            if (detachedMap == null)
                return CommandExecutionResult.Error("Can't detach root node");

            context.AppContext.Navigator.OpenCreateMap(detachedMap.RootNode.Name, detachedMap);
            return context.PersistMapChange("Detach node", relation: "from");
        }

        if (!context.Map.HasNode(parameters))
            return CommandExecutionResult.Error($"Can't find \"{parameters}\"");

        var mapToDetach = context.Map.DetachNodeAsNewMap(parameters);
        if (mapToDetach == null)
            return CommandExecutionResult.Error($"Couldn't detach \"{parameters}\"");

        context.AppContext.Navigator.OpenCreateMap(mapToDetach.RootNode.Name, mapToDetach);
        return context.PersistMapChange("Detach node", relation: "from");
    }

    private static CommandExecutionResult ProcessHide(EditCommandContext context, string parameters)
    {
        return context.Map.HideNode(parameters)
            ? context.PersistMapChange("Hide node")
            : CommandExecutionResult.Error($"Can't find \"{parameters}\"");
    }

    private static CommandExecutionResult ProcessUnhide(EditCommandContext context, string parameters)
    {
        return context.Map.UnhideNode(parameters)
            ? context.PersistMapChange("Unhide node")
            : CommandExecutionResult.Error($"Can't find \"{parameters}\"");
    }
}
