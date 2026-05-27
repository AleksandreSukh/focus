#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Systems.Sanity.Focus.Application;

internal interface IApplicationStatusSink
{
    void WriteBackgroundMessage(string message);

    void SetTitle(string? title);

    IDisposable ShowBusy(string message);
}

internal sealed class ConsoleApplicationStatusSink : IApplicationStatusSink
{
    private static readonly char[] BusyFrames = { '|', '/', '-', '\\' };

    public void WriteBackgroundMessage(string message)
    {
        AppConsole.Current.WriteBackgroundMessage(message);
    }

    public void SetTitle(string? title)
    {
        AppConsole.Current.SetTitle(title);
    }

    public IDisposable ShowBusy(string message)
    {
        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "Working..."
            : message.Trim();
        var cancellation = new CancellationTokenSource();
        var task = Task.Run(() => RunBusyLoop(normalizedMessage, cancellation.Token));
        return new BusyScope(cancellation, task, normalizedMessage);
    }

    private static void RunBusyLoop(string message, CancellationToken cancellationToken)
    {
        var frameIndex = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                AppConsole.Current.Write($"\r{BusyFrames[frameIndex % BusyFrames.Length]} {message}");
            }
            catch
            {
            }

            frameIndex++;

            try
            {
                Task.Delay(120, cancellationToken).Wait(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (AggregateException)
            {
            }
        }
    }

    private sealed class BusyScope : IDisposable
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly Task _task;
        private readonly int _clearLength;
        private bool _disposed;

        public BusyScope(CancellationTokenSource cancellation, Task task, string message)
        {
            _cancellation = cancellation;
            _task = task;
            _clearLength = message.Length + 2;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cancellation.Cancel();
            try
            {
                _task.Wait(TimeSpan.FromMilliseconds(250));
            }
            catch
            {
            }

            _cancellation.Dispose();
            try
            {
                AppConsole.Current.Write($"\r{new string(' ', _clearLength)}\r");
            }
            catch
            {
            }
        }
    }
}
