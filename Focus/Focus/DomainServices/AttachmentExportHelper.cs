#nullable enable

using System;
using System.IO;
using System.Text;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class AttachmentExportHelper
{
    public static AttachmentExportItem Build(Node node, NodeAttachment attachment, NodeExportOptions options)
    {
        var resolvedPath = TryResolveAttachmentPath(node, attachment, options);
        var relativePath = BuildRelativePath(attachment, resolvedPath, options);
        var displayName = PlainTextInlineFormatter.ToPlainText(attachment.DisplayName);

        if (IsPlainTextAttachment(attachment) &&
            TryReadAttachmentText(resolvedPath, out var textContent))
        {
            return new AttachmentExportItem(
                attachment,
                AttachmentExportKind.Text,
                relativePath,
                string.IsNullOrWhiteSpace(displayName) ? "Attachment" : displayName,
                textContent);
        }

        if (IsImageAttachment(attachment) && File.Exists(resolvedPath))
        {
            return new AttachmentExportItem(
                attachment,
                AttachmentExportKind.Image,
                relativePath,
                string.IsNullOrWhiteSpace(displayName) ? "Image attachment" : displayName,
                null);
        }

        return new AttachmentExportItem(
            attachment,
            AttachmentExportKind.Link,
            relativePath,
            string.IsNullOrWhiteSpace(displayName) ? relativePath : displayName,
            null);
    }

    private static bool TryReadAttachmentText(string? resolvedPath, out string textContent)
    {
        textContent = string.Empty;
        if (!File.Exists(resolvedPath))
            return false;

        try
        {
            textContent = File.ReadAllText(resolvedPath!, Encoding.UTF8);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildRelativePath(NodeAttachment attachment, string? resolvedPath, NodeExportOptions options)
    {
        var normalizedFallback = NormalizeForLinks(attachment.RelativePath);
        if (string.IsNullOrWhiteSpace(resolvedPath) || string.IsNullOrWhiteSpace(options.ExportFilePath))
            return normalizedFallback;

        var exportDirectory = Path.GetDirectoryName(options.ExportFilePath);
        if (string.IsNullOrWhiteSpace(exportDirectory))
            return normalizedFallback;

        try
        {
            return NormalizeForLinks(Path.GetRelativePath(exportDirectory, resolvedPath));
        }
        catch
        {
            return normalizedFallback;
        }
    }

    private static string? TryResolveAttachmentPath(Node node, NodeAttachment attachment, NodeExportOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MapFilePath) || !node.UniqueIdentifier.HasValue)
            return null;

        try
        {
            return new MapAttachmentStore().ResolveAttachmentPath(
                options.MapFilePath,
                node.UniqueIdentifier.Value,
                attachment.RelativePath);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPlainTextAttachment(NodeAttachment attachment) =>
        attachment.MediaType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase);

    private static bool IsImageAttachment(NodeAttachment attachment) =>
        attachment.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeForLinks(string? path) =>
        (path ?? string.Empty).Replace('\\', '/');
}

internal sealed record AttachmentExportItem(
    NodeAttachment Attachment,
    AttachmentExportKind Kind,
    string RelativePath,
    string DisplayName,
    string? TextContent);

internal enum AttachmentExportKind
{
    Text,
    Image,
    Link
}
