#nullable enable

using System;

namespace Systems.Sanity.Focus.Application;

public sealed class MapDeletionBlockedException : Exception
{
    public MapDeletionBlockedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public static MapDeletionBlockedException UnreadableMap(string fileName, Exception innerException) =>
        new(
            $"Map \"{fileName}\" cannot be deleted because it is unreadable and its attachment folders cannot be determined safely.",
            innerException);
}
