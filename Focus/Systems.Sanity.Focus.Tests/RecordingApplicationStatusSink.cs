using Systems.Sanity.Focus.Application;

namespace Systems.Sanity.Focus.Tests;

internal sealed class RecordingApplicationStatusSink : IApplicationStatusSink
{
    public List<string> BackgroundMessages { get; } = new();

    public List<string?> Titles { get; } = new();

    public List<string> BusyMessages { get; } = new();

    public int BusyDisposeCount { get; private set; }

    public void WriteBackgroundMessage(string message)
    {
        BackgroundMessages.Add(message);
    }

    public void SetTitle(string? title)
    {
        Titles.Add(title);
    }

    public IDisposable ShowBusy(string message)
    {
        BusyMessages.Add(message);
        return new RecordingBusyScope(this);
    }

    private sealed class RecordingBusyScope : IDisposable
    {
        private readonly RecordingApplicationStatusSink _owner;
        private bool _disposed;

        public RecordingBusyScope(RecordingApplicationStatusSink owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _owner.BusyDisposeCount++;
        }
    }
}
