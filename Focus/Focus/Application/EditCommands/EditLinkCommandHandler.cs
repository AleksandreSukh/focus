#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Application.Display;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditLinkCommandHandler : IEditCommandFeatureHandler
{
    public IReadOnlyCollection<EditCommandId> CommandIds { get; } = new[]
    {
        EditCommandId.LinkFrom,
        EditCommandId.LinkTo,
        EditCommandId.OpenLink,
        EditCommandId.Backlinks
    };

    public CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters) =>
        commandId switch
        {
            EditCommandId.LinkFrom => ProcessLinkFrom(context, parameters),
            EditCommandId.LinkTo => ProcessLinkTo(context, parameters),
            EditCommandId.OpenLink => ProcessOpenLink(context),
            EditCommandId.Backlinks => ProcessBacklinks(context),
            _ => CommandExecutionResult.Error($"Unsupported command \"{commandId}\"")
        };

    private static CommandExecutionResult ProcessLinkFrom(EditCommandContext context, string parameters)
    {
        var nodeToLink = context.Map.GetNode(parameters);
        if (nodeToLink == null)
            return CommandExecutionResult.Error($"Couldn't find node \"{parameters}\"");

        context.AppContext.LinkIndex.QueueLinkSource(nodeToLink);
        return CommandExecutionResult.Success();
    }

    private static CommandExecutionResult ProcessLinkTo(EditCommandContext context, string parameters)
    {
        if (!context.AppContext.LinkIndex.HasQueuedLinkSources)
            return CommandExecutionResult.Error("No source node selected. Use linkfrom first");

        if (!context.Map.HasNode(parameters))
            return CommandExecutionResult.Error($"Couldn't find node \"{parameters}\"");

        var targetNodePeek = context.Map.GetNodeContentPeekByIdentifier(parameters);
        var nodeToLinkFrom = context.AppContext.LinkIndex.PeekQueuedLinkSource();
        var selectedRelationType = SelectLinkRelationType(context);
        if (!selectedRelationType.HasValue)
            return CommandExecutionResult.Error("Cancelled!");

        if (!context.AppContext.WorkflowInteractions.Confirm(
                $"Link \"{NodeDisplayHelper.GetSingleLinePreview(nodeToLinkFrom.Name)}\" to \"{targetNodePeek}\" as \"{selectedRelationType.Value.ToDisplayString()}\"?"))
        {
            return CommandExecutionResult.Error("Cancelled!");
        }

        var linkResult = context.Map.LinkToNode(parameters, nodeToLinkFrom, selectedRelationType.Value, "Link Metadata");
        if (!linkResult)
            return CommandExecutionResult.Error($"Unexpected result while linking \"{parameters}\"");

        context.AppContext.LinkIndex.PopQueuedLinkSource();
        return context.PersistMapChange("Link nodes");
    }

    private static CommandExecutionResult ProcessOpenLink(EditCommandContext context)
    {
        var links = context.AppContext.LinkNavigationService.GetOutgoingLinks(context.Map.GetCurrentNode());
        if (!links.Any())
            return CommandExecutionResult.Error("Current node has no links");

        var sourceText = context.Map.GetCurrentNodeContentPeek();
        return NavigateToLinkedNode(context, links, $"Linked nodes — \"{sourceText}\"");
    }

    private static CommandExecutionResult ProcessBacklinks(EditCommandContext context)
    {
        var backlinks = context.AppContext.LinkNavigationService.GetBacklinks(context.Map.GetCurrentNode());
        if (!backlinks.Any())
            return CommandExecutionResult.Error("Current node has no backlinks");

        var sourceText = context.Map.GetCurrentNodeContentPeek();
        return NavigateToLinkedNode(context, backlinks, $"Backlinks — \"{sourceText}\"");
    }

    private static CommandExecutionResult NavigateToLinkedNode(
        EditCommandContext context,
        IReadOnlyList<NodeSearchResult> relatedNodes,
        string title)
    {
        var selectedResult = context.AppContext.WorkflowInteractions.SelectSearchResult(
            relatedNodes,
            title,
            new SearchResultDisplayOptions(
                includeMapName: true,
                colorizeAncestorPath: false,
                highlightTerms: Array.Empty<string>()));
        if (selectedResult == null)
            return CommandExecutionResult.Success();

        if (string.Equals(selectedResult.MapFilePath, context.FilePath, StringComparison.InvariantCultureIgnoreCase))
        {
            return context.Map.ChangeCurrentNodeById(selectedResult.NodeId)
                ? CommandExecutionResult.Success()
                : CommandExecutionResult.Error("Couldn't open selected node");
        }

        try
        {
            context.AppContext.Navigator.OpenEditMap(selectedResult.MapFilePath, selectedResult.NodeId);
            return CommandExecutionResult.Success();
        }
        catch (MapConflictAutoResolveException ex)
        {
            return CommandExecutionResult.Error(ex.Message);
        }
    }

    private static LinkRelationType? SelectLinkRelationType(EditCommandContext context)
    {
        var supportedTypes = LinkRelationTypeExtensions.GetSupportedTypes();
        var selectedOption = context.AppContext.WorkflowInteractions.SelectOption(
            supportedTypes.Select(type => type.ToDisplayString()).ToArray());
        if (selectedOption <= 0 || selectedOption > supportedTypes.Length)
            return null;

        return supportedTypes[selectedOption - 1];
    }
}
