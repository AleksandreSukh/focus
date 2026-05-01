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

    public string GetLegacyAttachmentDirectoryPath(string mapFilePath)
    {
        var mapDirectory = Path.GetDirectoryName(mapFilePath);
        if (string.IsNullOrWhiteSpace(mapDirectory))
            throw new ArgumentException("Map directory could not be determined.", nameof(mapFilePath));

        return Path.Combine(
            mapDirectory,
            $"{Path.GetFileNameWithoutExtension(mapFilePath)}{ConfigurationConstants.AttachmentDirectorySuffix}");
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

    public string ResolveLegacyAttachmentPath(string mapFilePath, string relativePath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        return Path.Combine(GetLegacyAttachmentDirectoryPath(mapFilePath), normalizedRelativePath);
    }

    public NodeAttachment SavePngAttachment(
        string mapFilePath,
        Guid nodeUniqueIdentifier,
        byte[] pngBytes,
        string displayName,
        DateTimeOffset? createdAtUtc = null)
    {
        return SaveBinaryAttachment(
            mapFilePath,
            nodeUniqueIdentifier,
            pngBytes,
            ".png",
            "image/png",
            displayName,
            createdAtUtc);
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

    public NodeAttachment SaveBinaryAttachment(
        string mapFilePath,
        Guid nodeUniqueIdentifier,
        byte[] bytes,
        string fileExtension,
        string mediaType,
        string displayName,
        DateTimeOffset? createdAtUtc = null)
    {
        if (bytes.Length == 0)
            throw new ArgumentException("Attachment bytes are empty.", nameof(bytes));

        var timestampUtc = (createdAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var normalizedExtension = NormalizeFileExtension(fileExtension);
        var fileName = $"{Guid.NewGuid():N}{normalizedExtension}";
        var attachmentDirectory = EnsureAttachmentDirectory(mapFilePath, nodeUniqueIdentifier);
        var targetPath = Path.Combine(attachmentDirectory, fileName);
        File.WriteAllBytes(targetPath, bytes);

        return new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = fileName,
            MediaType = string.IsNullOrWhiteSpace(mediaType) ? "application/octet-stream" : mediaType,
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

    public bool MigrateLegacyAttachments(string mapFilePath, MindMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var legacyDirectoryPath = GetLegacyAttachmentDirectoryPath(mapFilePath);
        if (!Directory.Exists(legacyDirectoryPath))
            return false;

        var didChange = false;
        var legacyReferences = EnumerateLegacyAttachmentReferences(mapFilePath, map.RootNode)
            .GroupBy(reference => reference.FileName, StringComparer.OrdinalIgnoreCase);

        foreach (var legacyReferenceGroup in legacyReferences)
        {
            var sourcePath = ResolveLegacyAttachmentPath(mapFilePath, legacyReferenceGroup.Key);
            if (!File.Exists(sourcePath))
                continue;

            var keepLegacySource = false;
            foreach (var legacyReference in legacyReferenceGroup)
            {
                if (File.Exists(legacyReference.TargetPath))
                {
                    keepLegacySource = true;
                    continue;
                }

                Directory.CreateDirectory(
                    Path.GetDirectoryName(legacyReference.TargetPath)
                    ?? throw new InvalidOperationException("Attachment directory could not be determined."));
                File.Copy(sourcePath, legacyReference.TargetPath, overwrite: false);
                didChange = true;
            }

            if (keepLegacySource || legacyReferenceGroup.Any(reference => !File.Exists(reference.TargetPath)))
                continue;

            File.Delete(sourcePath);
            didChange = true;
        }

        if (Directory.Exists(legacyDirectoryPath) &&
            !Directory.EnumerateFileSystemEntries(legacyDirectoryPath).Any())
        {
            Directory.Delete(legacyDirectoryPath, recursive: false);
            didChange = true;
        }

        return didChange;
    }

    public void DeleteAttachmentsForMap(string mapFilePath, MindMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        foreach (var nodeIdentifier in EnumerateNodeIdentifiers(map.RootNode).Distinct())
        {
            var attachmentDirectory = GetAttachmentDirectoryPath(mapFilePath, nodeIdentifier);
            if (Directory.Exists(attachmentDirectory))
            {
                Directory.Delete(attachmentDirectory, recursive: true);
            }
        }

        var legacyAttachmentDirectory = GetLegacyAttachmentDirectoryPath(mapFilePath);
        if (Directory.Exists(legacyAttachmentDirectory))
        {
            Directory.Delete(legacyAttachmentDirectory, recursive: true);
        }

        var attachmentRootDirectory = GetAttachmentRootDirectoryPath(mapFilePath);
        if (Directory.Exists(attachmentRootDirectory) &&
            !Directory.EnumerateFileSystemEntries(attachmentRootDirectory).Any())
        {
            Directory.Delete(attachmentRootDirectory, recursive: false);
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

    private IEnumerable<LegacyAttachmentReference> EnumerateLegacyAttachmentReferences(string mapFilePath, Node node)
    {
        if (node.Metadata?.Attachments != null && node.UniqueIdentifier.HasValue)
        {
            foreach (var attachment in node.Metadata.Attachments)
            {
                var normalizedRelativePath = NormalizeRelativePath(attachment.RelativePath);
                if (string.IsNullOrWhiteSpace(normalizedRelativePath))
                    continue;

                yield return new LegacyAttachmentReference(
                    normalizedRelativePath,
                    ResolveAttachmentPath(mapFilePath, node.UniqueIdentifier.Value, normalizedRelativePath));
            }
        }

        foreach (var childNode in node.Children)
        {
            foreach (var attachmentReference in EnumerateLegacyAttachmentReferences(mapFilePath, childNode))
            {
                yield return attachmentReference;
            }
        }
    }

    private IEnumerable<Guid> EnumerateNodeIdentifiers(Node node)
    {
        if (node.UniqueIdentifier.HasValue)
        {
            yield return node.UniqueIdentifier.Value;
        }

        foreach (var childNode in node.Children)
        {
            foreach (var childIdentifier in EnumerateNodeIdentifiers(childNode))
            {
                yield return childIdentifier;
            }
        }
    }

    private static string NormalizeRelativePath(string relativePath) =>
        Path.GetFileName(relativePath ?? string.Empty);

    private static string NormalizeFileExtension(string fileExtension)
    {
        var normalized = Path.GetFileName(fileExtension ?? string.Empty).Trim();
        normalized = normalized.TrimStart('.');
        if (string.IsNullOrWhiteSpace(normalized))
            return ".bin";

        var safeExtension = new string(normalized
            .Where(character => char.IsLetterOrDigit(character))
            .ToArray());
        return string.IsNullOrWhiteSpace(safeExtension)
            ? ".bin"
            : $".{safeExtension.ToLowerInvariant()}";
    }

    private static string NormalizeNodeDirectoryName(Guid nodeUniqueIdentifier) =>
        nodeUniqueIdentifier.ToString("D").ToLowerInvariant();

    private sealed record LegacyAttachmentReference(
        string FileName,
        string TargetPath);
}
