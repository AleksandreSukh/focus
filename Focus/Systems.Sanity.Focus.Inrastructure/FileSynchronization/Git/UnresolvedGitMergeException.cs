using System;
using System.Collections.Generic;
using System.Linq;

namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

internal sealed class UnresolvedGitMergeException : InvalidOperationException
{
    public UnresolvedGitMergeException(IReadOnlyCollection<string> unresolvedFiles)
        : base(BuildMessage(unresolvedFiles))
    {
        UnresolvedFiles = unresolvedFiles.ToArray();
    }

    public IReadOnlyList<string> UnresolvedFiles { get; }

    private static string BuildMessage(IReadOnlyCollection<string> unresolvedFiles)
    {
        if (unresolvedFiles.Count == 0)
            return "Git merge still has unresolved files. Resolve them manually or finish the merge before syncing.";

        return $"Git merge still has unresolved files: {string.Join(", ", unresolvedFiles.OrderBy(file => file, StringComparer.OrdinalIgnoreCase))}. Resolve them manually or finish the merge before syncing.";
    }
}
