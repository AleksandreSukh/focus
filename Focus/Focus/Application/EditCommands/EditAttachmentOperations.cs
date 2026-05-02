#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal static class EditAttachmentOperations
{
    public static IReadOnlyList<NodeAttachment> GetCurrentAttachments(EditCommandContext context) =>
        context.Map.GetCurrentNode().Metadata?.Attachments ?? new List<NodeAttachment>();

    public static IEnumerable<string> GetCurrentAttachmentSelectors(EditCommandContext context)
    {
        var attachments = GetCurrentAttachments(context);
        for (var index = 1; index <= attachments.Count; index++)
        {
            yield return index.ToString();

            var shortcut = AccessibleKeyNumbering.GetStringFor(index);
            if (!string.IsNullOrWhiteSpace(shortcut))
                yield return shortcut;
        }
    }

    public static string BuildCurrentAttachmentSummary(EditCommandContext context)
    {
        var attachments = GetCurrentAttachments(context);
        if (attachments.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        builder.Append(":Attachments> ");
        for (var index = 0; index < attachments.Count; index++)
        {
            if (index > 0)
                builder.Append("; ");

            builder.Append(BuildAttachmentAddressMarkup(index + 1));
            builder.Append(' ');
            builder.Append(NodeDisplayHelper.GetContentPeek(attachments[index].DisplayName));
        }

        builder.AppendLine();
        builder.AppendLine();
        return builder.ToString();
    }

    public static bool TryOpenCurrentAttachmentShortcut(
        EditCommandContext context,
        string command,
        out CommandExecutionResult result)
    {
        result = CommandExecutionResult.Success();
        var attachments = GetCurrentAttachments(context);
        if (attachments.Count == 0 || string.IsNullOrWhiteSpace(command))
            return false;

        if (!TryGetAttachmentIndex(command, out var attachmentIndex))
            return false;

        if (attachmentIndex > attachments.Count)
        {
            result = CommandExecutionResult.Error($"Can't find attachment \"{command}\"");
            return true;
        }

        result = OpenAttachment(context, attachments[attachmentIndex - 1]);
        return true;
    }

    public static CommandExecutionResult ProcessAttachments(EditCommandContext context, string parameters)
    {
        try
        {
            var attachments = GetCurrentAttachments(context);
            if (attachments.Count == 0)
                return CommandExecutionResult.Error("Current node has no attachments");

            if (string.IsNullOrWhiteSpace(parameters))
            {
                return attachments.Count == 1
                    ? OpenAttachment(context, attachments[0])
                    : CommandExecutionResult.Error(
                        $"Specify attachment shortcut: {string.Join(", ", GetCurrentAttachmentSelectors(context))}");
            }

            return TryResolveCurrentAttachment(context, parameters, out var attachment, out var errorMessage)
                ? OpenAttachment(context, attachment!)
                : CommandExecutionResult.Error(errorMessage!);
        }
        catch (Exception ex)
        {
            return CommandExecutionResult.Error(ExceptionDiagnostics.LogException(ex, "opening attachment"));
        }
    }

    public static Guid GetRequiredNodeIdentifier(Node node) =>
        node.UniqueIdentifier ?? throw new InvalidOperationException("Node identifier is required for attachment operations.");

    private static bool TryResolveCurrentAttachment(
        EditCommandContext context,
        string parameters,
        out NodeAttachment? attachment,
        out string? errorMessage)
    {
        attachment = null;
        var attachments = GetCurrentAttachments(context);
        if (attachments.Count == 0)
        {
            errorMessage = "Current node has no attachments";
            return false;
        }

        var normalizedParameters = parameters.Trim().ToCommandKey();
        if (!TryGetAttachmentIndex(normalizedParameters, out var attachmentIndex))
        {
            errorMessage = $"Unknown attachment \"{parameters}\". Use a number or shortcut like \"{BuildAttachmentAddress(1)}\".";
            return false;
        }

        if (attachmentIndex > attachments.Count)
        {
            errorMessage = $"Can't find attachment \"{parameters}\"";
            return false;
        }

        attachment = attachments[attachmentIndex - 1];
        errorMessage = null;
        return true;
    }

    private static bool TryGetAttachmentIndex(string value, out int attachmentIndex)
    {
        if (int.TryParse(value, out attachmentIndex) && attachmentIndex > 0)
            return true;

        attachmentIndex = AccessibleKeyNumbering.GetNumberFor(value);
        return attachmentIndex > 0;
    }

    private static CommandExecutionResult OpenAttachment(EditCommandContext context, NodeAttachment attachment)
    {
        var currentNodeIdentifier = GetRequiredNodeIdentifier(context.Map.GetCurrentNode());
        var attachmentPath = context.AppContext.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            context.FilePath,
            currentNodeIdentifier,
            attachment.RelativePath);
        if (!File.Exists(attachmentPath))
            return CommandExecutionResult.Error($"Attachment \"{attachment.DisplayName}\" is missing");

        return context.AppContext.FileOpener.TryOpen(attachmentPath, out var openErrorMessage)
            ? CommandExecutionResult.Success($"Opened attachment \"{attachment.DisplayName}\"")
            : CommandExecutionResult.Error(openErrorMessage ?? "The attachment could not be opened.");
    }

    private static string BuildAttachmentAddress(int index)
    {
        var shortcut = AccessibleKeyNumbering.GetStringFor(index);
        return string.IsNullOrWhiteSpace(shortcut)
            ? index.ToString()
            : $"{shortcut}/{index}";
    }

    private static string BuildAttachmentAddressMarkup(int index)
    {
        var shortcut = AccessibleKeyNumbering.GetStringFor(index);
        return string.IsNullOrWhiteSpace(shortcut)
            ? $"[{ConfigurationConstants.CommandColor}]{index}[!]"
            : $"[{ConfigurationConstants.CommandColor}]{shortcut}[!]/[{ConfigurationConstants.CommandColor}]{index}[!]";
    }
}
