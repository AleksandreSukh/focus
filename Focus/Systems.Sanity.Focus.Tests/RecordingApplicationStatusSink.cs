using Systems.Sanity.Focus.Application;

namespace Systems.Sanity.Focus.Tests;

internal sealed class RecordingApplicationStatusSink : IApplicationStatusSink
{
    public List<string> BackgroundMessages { get; } = new();

    public List<string?> Titles { get; } = new();

    public void WriteBackgroundMessage(string message)
    {
        BackgroundMessages.Add(message);
    }

    public void SetTitle(string? title)
    {
        Titles.Add(title);
    }
}
