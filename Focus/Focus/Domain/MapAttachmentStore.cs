#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Systems.Sanity.Focus.Domain;

internal sealed class MapAttachmentStore
{
    public string GetAttachmentRootDirectoryPath(string mapFilePath)
    {
        var mapDirectory = Path.GetDirectoryName(mapFilePath);
        if (string.IsNullOrWhiteSpace(mapDirectory))
            throw new ArgumentException("Map directory could not be determined.", nameof(mapFilePath));

        return Path.Combine(mapDirectory, ConfigurationConstants.AttachmentDirectorySuffix);
    }

    public string GetAttachmentDirectoryPath(string mapFilePath, Guid nodeUniqueIdentifier) =>
        Path.Combine(
            GetAttachmentRootDirectoryPath(mapFilePath),
            NormalizeNodeDirectoryName(nodeUniqueIdentifier));

    public string ResolveAttachmentPath(string mapFilePath, Guid nodeUniqueIdentifier, string relativePath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        return Path.Combine(GetAttachmentDirectoryPath(mapFilePath, nodeUniqueIdentifier), normalizedRelativePath);
    }

    public NodeAttachment SavePngAttachment(
        string mapFilePath,
        Guid nodeUniqueIdentifier,
        byte[] pngBytes,
        string displayName,
        DateTimeOffset? createdAtUtc = null)
    {
        if (pngBytes.Length == 0)
            throw new ArgumentException("Attachment bytes are empty.", nameof(pngBytes));

        var timestampUtc = (createdAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var fileName = $"{Guid.NewGuid():N}.png";
        var attachmentDirectory = EnsureAttachmentDirectory(mapFilePath, nodeUniqueIdentifier);
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
        Guid nodeUniqueIdentifier,
        string text,
        string displayName,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var timestampUtc = (createdAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var fileName = $"{Guid.NewGuid():N}.txt";
        var attachmentDirectory = EnsureAttachmentDirectory(mapFilePath, nodeUniqueIdentifier);
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

    public void MoveAttachmentDirectories(string mapFilePath, IReadOnlyDictionary<Guid, Guid> remappedIdentifiers)
    {
        foreach (var remappedIdentifier in remappedIdentifiers)
        {
            MoveAttachmentDirectory(mapFilePath, remappedIdentifier.Key, remappedIdentifier.Value);
        }
    }

    private void MoveAttachmentDirectory(string mapFilePath, Guid existingNodeIdentifier, Guid newNodeIdentifier)
    {
        if (existingNodeIdentifier == newNodeIdentifier)
            return;

        var sourceDirectory = GetAttachmentDirectoryPath(mapFilePath, existingNodeIdentifier);
        if (!Directory.Exists(sourceDirectory))
            return;

        var targetDirectory = GetAttachmentDirectoryPath(mapFilePath, newNodeIdentifier);
        if (string.Equals(sourceDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(targetDirectory)
            ?? throw new InvalidOperationException("Target attachment directory could not be determined."));

        if (!Directory.Exists(targetDirectory))
        {
            Directory.Move(sourceDirectory, targetDirectory);
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(filePath));
            File.Move(filePath, targetPath, overwrite: true);
        }

        if (!Directory.EnumerateFileSystemEntries(sourceDirectory).Any())
            Directory.Delete(sourceDirectory, recursive: false);
    }

    private string EnsureAttachmentDirectory(string mapFilePath, Guid nodeUniqueIdentifier)
    {
        var attachmentDirectory = GetAttachmentDirectoryPath(mapFilePath, nodeUniqueIdentifier);
        Directory.CreateDirectory(attachmentDirectory);
        return attachmentDirectory;
    }

    private static string NormalizeRelativePath(string relativePath) =>
        Path.GetFileName(relativePath ?? string.Empty);

    private static string NormalizeNodeDirectoryName(Guid nodeUniqueIdentifier) =>
        nodeUniqueIdentifier.ToString("D").ToLowerInvariant();
}
