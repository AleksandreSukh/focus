#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditExportCommandHandler : IEditCommandFeatureHandler
{
    public IReadOnlyCollection<EditCommandId> CommandIds { get; } = new[]
    {
        EditCommandId.Export
    };

    public CommandExecutionResult Execute(EditCommandContext context, EditCommandId commandId, string parameters) =>
        commandId switch
        {
            EditCommandId.Export => ProcessExport(context),
            _ => CommandExecutionResult.Error($"Unsupported command \"{commandId}\"")
        };

    private static CommandExecutionResult ProcessExport(EditCommandContext context)
    {
        try
        {
            var exportRequest = context.AppContext.WorkflowInteractions.SelectExport(
                MapFilePathHelper.SanitizeFileName(context.Map.GetCurrentNodeName(), fallbackFileName: "export"));

            if (exportRequest == null)
                return CommandExecutionResult.Success();

            return ProcessExport(context, exportRequest);
        }
        catch (Exception ex)
        {
            return CommandExecutionResult.Error(ExceptionDiagnostics.LogException(ex, "exporting map"));
        }
    }

    private static CommandExecutionResult ProcessExport(EditCommandContext context, ExportRequest exportRequest)
    {
        if (exportRequest.Destination == ExportDestination.ClipboardText)
            return ProcessCopyTextExport(context, exportRequest);

        var exportDirectory = Path.GetDirectoryName(context.FilePath);
        if (string.IsNullOrWhiteSpace(exportDirectory))
            return CommandExecutionResult.Error("Couldn't determine export folder");

        var targetFileName = MapFilePathHelper.SanitizeFileName(
            string.IsNullOrWhiteSpace(exportRequest.FileName) ? context.Map.GetCurrentNodeName() : exportRequest.FileName,
            fallbackFileName: "export");

        try
        {
            var exportedFilePath = context.AppContext.WorkflowInteractions.RequestAvailableFilePath(
                exportDirectory,
                targetFileName,
                exportRequest.Format.GetFileExtension());
            if (exportedFilePath == null)
                return CommandExecutionResult.Success("Export cancelled");

            var exportedContent = MapExportService.Export(
                context.Map.GetCurrentNode(),
                exportRequest.Format,
                new NodeExportOptions(
                    SkipCollapsedDescendants: exportRequest.SkipCollapsedDescendants,
                    UseBlackBackground: exportRequest.UseBlackBackground,
                    IncludeAttachments: exportRequest.IncludeAttachments,
                    MapFilePath: context.FilePath,
                    ExportFilePath: exportedFilePath));
            File.WriteAllText(exportedFilePath, exportedContent);

            context.AppContext.MapsStorage.Sync(BuildExportSyncCommitMessage(context, exportRequest.Format));
            var collapsedSuffix = exportRequest.SkipCollapsedDescendants ? " (collapsed descendants skipped)" : string.Empty;
            return CommandExecutionResult.Success(
                $"Exported {exportRequest.Format.ToExportVerb()} to \"{Path.GetFileName(exportedFilePath)}\"{collapsedSuffix}");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            return CommandExecutionResult.Error(ExceptionDiagnostics.LogException(ex, "exporting map"));
        }
    }

    private static CommandExecutionResult ProcessCopyTextExport(EditCommandContext context, ExportRequest exportRequest)
    {
        var exportedContent = MapExportService.Export(
            context.Map.GetCurrentNode(),
            ExportFormat.PlainText,
            new NodeExportOptions(SkipCollapsedDescendants: exportRequest.SkipCollapsedDescendants));
        var copyResult = context.AppContext.ClipboardTextWriter.CopyText(exportedContent);
        if (!copyResult.IsSuccess)
            return CommandExecutionResult.Error(copyResult.ErrorMessage ?? "Couldn't copy text to clipboard.");

        var collapsedSuffix = exportRequest.SkipCollapsedDescendants ? " (collapsed descendants skipped)" : string.Empty;
        return CommandExecutionResult.Success($"Copied plain text export to clipboard{collapsedSuffix}");
    }

    private static string BuildExportSyncCommitMessage(EditCommandContext context, ExportFormat format) =>
        format == ExportFormat.Html
            ? context.BuildMapCommitMessage("Export HTML", "from")
            : context.BuildMapCommitMessage("Export markdown", "from");
}
