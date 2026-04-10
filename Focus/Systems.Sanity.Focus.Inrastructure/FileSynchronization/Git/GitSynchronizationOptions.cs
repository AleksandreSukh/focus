using System;

namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

public sealed record GitSynchronizationOptions(bool RunInBackground, TimeSpan SynchronizationDelay)
{
    public static GitSynchronizationOptions BackgroundDebounced { get; } =
        new(RunInBackground: true, SynchronizationDelay: TimeSpan.FromSeconds(5));

    public static GitSynchronizationOptions Immediate { get; } =
        new(RunInBackground: false, SynchronizationDelay: TimeSpan.Zero);
}
