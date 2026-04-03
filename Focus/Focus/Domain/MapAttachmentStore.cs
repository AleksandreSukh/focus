#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Systems.Sanity.Focus.Domain;

internal sealed class MapAttachmentStore
{
    public string GetAttachmentDirectoryPath(string mapFilePath)
    {
        var mapDirectory = Path.GetDirectoryName(mapFilePath);
        if (string.IsNullOrWhiteSpace(mapDirectory))
            throw new ArgumentException("Map directory could not be determined.", nameof(mapFilePath));

        return Path.Combine(
            mapDirectory,
            $"{Path.GetFileNameWithoutExtension(mapFilePath)}{ConfigurationConstants.AttachmentDirectorySuffix}");
    }

    public string ResolveAttachmentPath(string mapFilePath, string relativePath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        return Path.Combine(GetAttachmentDirectoryPath(mapFilePath), normalizedRelativePath);
    }

    public NodeAttachment SavePngAttachment(
        string mapFilePath,
        byte[] pngBytes,
        string displayName,
        DateTimeOffset? createdAtUtc = null)
    {
        if (pngBytes.Length == 0)
            throw new ArgumentException("Attachment bytes are empty.", nameof(pngBytes));

        var timestampUtc = (createdAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var fileName = $"{Guid.NewGuid():N}.png";
        var attachmentDirectory = EnsureAttachmentDirectory(mapFilePath);
        var targetPath = Path.Combine(attachmentDirectory, fileName);
        File.WriteAllBytes(targetPath, pngBytes);

        return new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = fileName,
            MediaType = "image/png",
            DisplayName = displayName,
            CreatedAtUtc = timestampUtc
        };
    }

    public NodeAttachment SaveTextAttachment(
        string mapFilePath,
        string text,
        string displayName,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var timestampUtc = (createdAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var fileName = $"{Guid.NewGuid():N}.txt";
        var attachmentDirectory = EnsureAttachmentDirectory(mapFilePath);
        var targetPath = Path.Combine(attachmentDirectory, fileName);
        File.WriteAllText(targetPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = fileName,
            MediaType = "text/plain; charset=utf-8",
            DisplayName = displayName,
            CreatedAtUtc = timestampUtc
        };
    }

    public void DeleteAttachmentDirectory(string mapFilePath)
    {
        var attachmentDirectory = GetAttachmentDirectoryPath(mapFilePath);
        if (Directory.Exists(attachmentDirectory))
            Directory.Delete(attachmentDirectory, recursive: true);
    }

    public void RenameAttachmentDirectory(string existingMapFilePath, string newMapFilePath)
    {
        var sourceDirectory = GetAttachmentDirectoryPath(existingMapFilePath);
        if (!Directory.Exists(sourceDirectory))
            return;

        var targetDirectory = GetAttachmentDirectoryPath(newMapFilePath);
        if (string.Equals(sourceDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(targetDirectory)
            ?? throw new InvalidOperationException("Target attachment directory could not be determined."));

        if (!Directory.Exists(targetDirectory))
        {
            Directory.Move(sourceDirectory, targetDirectory);
            return;
        }

        foreach (var filePath in Directory.GetFiles(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(filePath));
            File.Move(filePath, targetPath, overwrite: true);
        }

        if (!Directory.EnumerateFileSystemEntries(sourceDirectory).Any())
            Directory.Delete(sourceDirectory, recursive: false);
    }

    public void MoveReferencedAttachments(Node node, string sourceMapFilePath, string targetMapFilePath)
    {
        if (string.Equals(sourceMapFilePath, targetMapFilePath, StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var currentNode in Traverse(node))
        {
            var attachments = currentNode.Metadata?.Attachments;
            if (attachments == null || attachments.Count == 0)
                continue;

            foreach (var attachment in attachments)
            {
                attachment.RelativePath = MoveAttachment(
                    sourceMapFilePath,
                    targetMapFilePath,
                    attachment.RelativePath);
            }

            currentNode.TouchMetadata();
        }

        DeleteAttachmentDirectoryIfEmpty(sourceMapFilePath);
    }

    private string MoveAttachment(string sourceMapFilePath, string targetMapFilePath, string relativePath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var sourcePath = ResolveAttachmentPath(sourceMapFilePath, normalizedRelativePath);
        if (!File.Exists(sourcePath))
            return normalizedRelativePath;

        var targetDirectory = EnsureAttachmentDirectory(targetMapFilePath);
        var targetFileName = normalizedRelativePath;
        var targetPath = Path.Combine(targetDirectory, targetFileName);

        if (!string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase) && File.Exists(targetPath))
        {
            targetFileName = $"{Guid.NewGuid():N}{Path.GetExtension(normalizedRelativePath)}";
            targetPath = Path.Combine(targetDirectory, targetFileName);
        }

        if (!string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            File.Move(sourcePath, targetPath, overwrite: false);

        return targetFileName;
    }

    private string EnsureAttachmentDirectory(string mapFilePath)
    {
        var attachmentDirectory = GetAttachmentDirectoryPath(mapFilePath);
        Directory.CreateDirectory(attachmentDirectory);
        return attachmentDirectory;
    }

    private void DeleteAttachmentDirectoryIfEmpty(string mapFilePath)
    {
        var attachmentDirectory = GetAttachmentDirectoryPath(mapFilePath);
        if (Directory.Exists(attachmentDirectory) &&
            !Directory.EnumerateFileSystemEntries(attachmentDirectory).Any())
        {
            Directory.Delete(attachmentDirectory, recursive: false);
        }
    }

    private static string NormalizeRelativePath(string relativePath) =>
        Path.GetFileName(relativePath ?? string.Empty);

    private static IEnumerable<Node> Traverse(Node rootNode)
    {
        yield return rootNode;

        foreach (var childNode in rootNode.Children)
        {
            foreach (var nestedNode in Traverse(childNode))
            {
                yield return nestedNode;
            }
        }
    }
}
