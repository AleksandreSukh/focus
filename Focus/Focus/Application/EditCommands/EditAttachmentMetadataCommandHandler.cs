#nullable enable

using System.Collections.Generic;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditAttachmentMetadataCommandHandler : IEditCommandFeatureHandler
{
    public IReadOnlyCollection<EditCommandId> CommandIds { get; } = new[]
    {
        EditCommandId.Attachments,
        EditCommandId.Meta
    };

    public CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters) =>
        commandId switch
        {
            EditCommandId.Attachments => EditAttachmentOperations.ProcessAttachments(context, parameters),
            EditCommandId.Meta => ProcessMeta(context, parameters),
            _ => CommandExecutionResult.Error($"Unsupported command \"{commandId}\"")
        };

    private static CommandExecutionResult ProcessMeta(EditCommandContext context, string parameters)
    {
        if (!TryResolveNodeForMetadataCommand(context, parameters, out var node, out var errorMessage))
            return CommandExecutionResult.Error(errorMessage!);

        var resolvedNode = node!;
        context.AppContext.WorkflowInteractions.ShowNodeMetadata(
            resolvedNode,
            $"Metadata for {NodeDisplayHelper.GetContentPeek(resolvedNode.Name)}");
        return CommandExecutionResult.Success();
    }

    private static bool TryResolveNodeForMetadataCommand(
        EditCommandContext context,
        string parameters,
        out Node? node,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            node = context.Map.GetCurrentNode();
            errorMessage = null;
            return true;
        }

        node = context.Map.GetNode(parameters);
        errorMessage = node == null
            ? $"Can't find \"{parameters}\""
            : null;
        return node != null;
    }
}
