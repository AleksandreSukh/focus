using System;
using System.Collections.Generic;

namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization;

public enum MergeRecoveryStatus
{
    NoAction,
    FileStaged,
    MergeCommitted,
    UnresolvedFilesRemain
}

public readonly record struct MergeRecoveryResult(
    MergeRecoveryStatus Status,
    IReadOnlyList<string> RemainingUnmergedFiles)
{
    public static MergeRecoveryResult NoAction { get; } =
        new(MergeRecoveryStatus.NoAction, Array.Empty<string>());

    public static MergeRecoveryResult MergeCommitted { get; } =
        new(MergeRecoveryStatus.MergeCommitted, Array.Empty<string>());

    public static MergeRecoveryResult FileStagedResult(IReadOnlyList<string> remainingUnmergedFiles) =>
        new(MergeRecoveryStatus.FileStaged, CopyRemainingFiles(remainingUnmergedFiles));

    public static MergeRecoveryResult UnresolvedFilesRemainResult(IReadOnlyList<string> remainingUnmergedFiles) =>
        new(MergeRecoveryStatus.UnresolvedFilesRemain, CopyRemainingFiles(remainingUnmergedFiles));

    private static IReadOnlyList<string> CopyRemainingFiles(IReadOnlyList<string> remainingUnmergedFiles)
    {
        if (remainingUnmergedFiles.Count == 0)
            return Array.Empty<string>();

        var copy = new string[remainingUnmergedFiles.Count];
        for (var index = 0; index < remainingUnmergedFiles.Count; index++)
        {
            copy[index] = remainingUnmergedFiles[index];
        }

        return copy;
    }
}
