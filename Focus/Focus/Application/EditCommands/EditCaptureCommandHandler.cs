#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Systems.Sanity.Focus.Application.WorkflowInteractions;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditCaptureCommandHandler : IEditCommandFeatureHandler
{
    private const int ClipboardTextPreviewMinLength = 20;
    private const int ClipboardTextPreviewMaxLength = 30;
    private static readonly TimeSpan VoiceRecordingMaxDuration = TimeSpan.FromMinutes(5);

    public IReadOnlyCollection<EditCommandId> CommandIds { get; } = new[]
    {
        EditCommandId.Capture,
        EditCommandId.Voice
    };

    public CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters) =>
        commandId switch
        {
            EditCommandId.Capture => ProcessCapture(context),
            EditCommandId.Voice => ProcessVoice(context),
            _ => CommandExecutionResult.Error($"Unsupported command \"{commandId}\"")
        };

    private static CommandExecutionResult ProcessCapture(EditCommandContext context)
    {
        try
        {
            var captureResult = context.AppContext.ClipboardCaptureService.Capture();
            if (!captureResult.IsSuccess)
                return CommandExecutionResult.Error(captureResult.ErrorMessage ?? "Clipboard capture failed");

            var currentNode = context.Map.GetCurrentNode();
            var currentNodeIdentifier = EditAttachmentOperations.GetRequiredNodeIdentifier(currentNode);

            if (captureResult.Kind == ClipboardCaptureKind.Image)
            {
                var timestampUtc = DateTimeOffset.UtcNow;
                var attachment = context.AppContext.MapsStorage.AttachmentStore.SavePngAttachment(
                    context.FilePath,
                    currentNodeIdentifier,
                    captureResult.ImageBytes ?? Array.Empty<byte>(),
                    BuildClipboardImageAttachmentDisplayName(timestampUtc),
                    timestampUtc);
                currentNode.AddAttachment(attachment, timestampUtc);

                return context.PersistMapChange(
                    "Capture clipboard",
                    message: $"Captured clipboard image into \"{NodeDisplayHelper.GetContentPeek(currentNode.Name)}\"");
            }

            var capturedAtUtc = DateTimeOffset.UtcNow;
            var capturedText = captureResult.Text ?? string.Empty;
            var textAttachment = context.AppContext.MapsStorage.AttachmentStore.SaveTextAttachment(
                context.FilePath,
                currentNodeIdentifier,
                capturedText,
                BuildClipboardTextAttachmentDisplayName(capturedText, capturedAtUtc),
                capturedAtUtc);
            currentNode.AddAttachment(textAttachment, capturedAtUtc);

            return context.PersistMapChange(
                "Capture clipboard",
                message: $"Captured clipboard text into \"{NodeDisplayHelper.GetContentPeek(currentNode.Name)}\"");
        }
        catch (Exception ex)
        {
            return CommandExecutionResult.Error(ExceptionDiagnostics.LogException(ex, "capturing clipboard"));
        }
    }

    private static CommandExecutionResult ProcessVoice(EditCommandContext context)
    {
        var startResult = context.AppContext.VoiceRecorder.Start(new VoiceRecordingOptions(VoiceRecordingMaxDuration));
        if (!startResult.IsSuccess)
            return CommandExecutionResult.Error(startResult.ErrorMessage ?? "Couldn't start voice recording.");

        var recordingStartedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            var decision = context.AppContext.WorkflowInteractions.WaitForVoiceRecordingDecision(
                recordingStartedAtUtc,
                VoiceRecordingMaxDuration);
            if (decision == WorkflowVoiceRecordingDecision.Cancel)
            {
                var cancelResult = context.AppContext.VoiceRecorder.CancelAsync().GetAwaiter().GetResult();
                return cancelResult.IsSuccess
                    ? CommandExecutionResult.Success("Voice note cancelled")
                    : CommandExecutionResult.Error(cancelResult.ErrorMessage ?? "Couldn't cancel voice recording.");
            }

            var recordingResult = context.AppContext.VoiceRecorder.StopAsync().GetAwaiter().GetResult();
            if (!recordingResult.IsSuccess || string.IsNullOrWhiteSpace(recordingResult.FilePath))
                return CommandExecutionResult.Error(recordingResult.ErrorMessage ?? "Couldn't save voice recording.");

            try
            {
                var capturedAtUtc = DateTimeOffset.UtcNow;
                var currentNode = context.Map.GetCurrentNode();
                var currentNodeIdentifier = EditAttachmentOperations.GetRequiredNodeIdentifier(currentNode);
                var attachment = context.AppContext.MapsStorage.AttachmentStore.SaveBinaryAttachment(
                    context.FilePath,
                    currentNodeIdentifier,
                    File.ReadAllBytes(recordingResult.FilePath),
                    recordingResult.FileExtension,
                    recordingResult.MediaType,
                    BuildVoiceNoteAttachmentDisplayName(capturedAtUtc),
                    capturedAtUtc);
                currentNode.AddAttachment(attachment, capturedAtUtc);

                var capSuffix = decision == WorkflowVoiceRecordingDecision.TimeLimitReached
                    ? " Recording stopped at the 5 minute limit."
                    : string.Empty;
                return context.PersistMapChange(
                    "Capture voice note",
                    message: $"Captured voice note into \"{NodeDisplayHelper.GetContentPeek(currentNode.Name)}\".{capSuffix}");
            }
            finally
            {
                DeleteFileIfExists(recordingResult.FilePath);
            }
        }
        catch (Exception ex)
        {
            TryCancelVoiceRecording(context);
            return CommandExecutionResult.Error(ExceptionDiagnostics.LogException(ex, "capturing voice note"));
        }
    }

    private static void DeleteFileIfExists(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            File.Delete(filePath);
    }

    private static void TryCancelVoiceRecording(EditCommandContext context)
    {
        try
        {
            context.AppContext.VoiceRecorder.CancelAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }
    }

    private static string BuildVoiceNoteAttachmentDisplayName(DateTimeOffset timestampUtc) =>
        $"Voice note {timestampUtc:yyyy-MM-dd HH:mm}";

    private static string BuildClipboardImageAttachmentDisplayName(DateTimeOffset timestampUtc) =>
        $"Screenshot {timestampUtc:yyyy-MM-dd HH:mm}.png";

    private static string BuildClipboardTextAttachmentDisplayName(string clipboardText, DateTimeOffset timestampUtc) =>
        $"Clipboard text {timestampUtc:yyyy-MM-dd HH:mm} - {BuildClipboardTextPreview(clipboardText)}";

    private static string BuildClipboardTextPreview(string clipboardText)
    {
        var normalizedText = NormalizeWhitespace(clipboardText);
        if (string.IsNullOrWhiteSpace(normalizedText))
            return "Clipboard text...";

        if (normalizedText.Length <= ClipboardTextPreviewMaxLength)
            return normalizedText.EndsWith("...", StringComparison.Ordinal)
                ? normalizedText
                : $"{normalizedText}...";

        var searchLength = Math.Min(ClipboardTextPreviewMaxLength, normalizedText.Length);
        var preferredBreakIndex = normalizedText.LastIndexOf(' ', searchLength - 1, searchLength);
        if (preferredBreakIndex >= ClipboardTextPreviewMinLength)
            return $"{normalizedText[..preferredBreakIndex].TrimEnd()}...";

        return $"{normalizedText[..ClipboardTextPreviewMaxLength].TrimEnd()}...";
    }

    private static string NormalizeWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (builder.Length == 0 || previousWasWhitespace)
                    continue;

                builder.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }
}
