#nullable enable

using System;

namespace Systems.Sanity.Focus.Domain;

public sealed class MapConflictAutoResolveException : InvalidOperationException
{
    public const string DefaultMessage =
        "This map has merge conflicts that couldn't be auto-resolved. Resolve the file manually or finish the merge first.";

    public MapConflictAutoResolveException()
        : base(DefaultMessage)
    {
    }

    public MapConflictAutoResolveException(string message)
        : base(message)
    {
    }
}
