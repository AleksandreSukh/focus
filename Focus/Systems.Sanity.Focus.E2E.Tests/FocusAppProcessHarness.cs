using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.E2E.Tests;

internal sealed class FocusAppProcessHarness : IAsyncDisposable
{
    private readonly FocusE2EWorkspace _workspace;
    private readonly FocusTestHostClient _testHostClient;
    private readonly string _pipeName;
    private readonly StringBuilder _standardOutput = new();
    private readonly StringBuilder _standardError = new();
    private readonly object _transcriptLock = new();
    private readonly CancellationTokenSource _cancellation = new();

    private Process? _process;
    private Task? _standardOutputPump;
    private Task? _standardErrorPump;

    public FocusAppProcessHarness(FocusE2EWorkspace workspace)
    {
        _workspace = workspace;
        _pipeName = $"focus-e2e-{Guid.NewGuid():N}";
        _testHostClient = new FocusTestHostClient(_pipeName);
    }

    public async Task StartAsync()
    {
        var appAssemblyPath = typeof(UserConfig).Assembly.Location;
        var workingDirectory = Path.GetDirectoryName(appAssemblyPath)
            ?? throw new InvalidOperationException("Couldn't determine app assembly directory.");
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(appAssemblyPath);
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add(_workspace.ConfigFilePath);
        startInfo.ArgumentList.Add("--test-host");
        startInfo.ArgumentList.Add(_pipeName);
        startInfo.Environment["USERPROFILE"] = _workspace.HomeDirectory;
        startInfo.Environment["HOME"] = _workspace.HomeDirectory;

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Focus app process.");
        _standardOutputPump = PumpStreamAsync(_process.StandardOutput, _standardOutput, _cancellation.Token);
        _standardErrorPump = PumpStreamAsync(_process.StandardError, _standardError, _cancellation.Token);

        await _testHostClient.ConnectAsync();
    }

    public Task SendLineAsync(string text, int echoDelayMs = 0) =>
        _testHostClient.SendLineAsync(text, echoDelayMs);

    public Task SendKeyAsync(ConsoleKeyInfo keyInfo) =>
        _testHostClient.SendKeyAsync(keyInfo);

    public async Task WaitForOutputAsync(string expectedText, TimeSpan? timeout = null)
    {
        var deadlineUtc = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow <= deadlineUtc)
        {
            if (GetTranscript().Contains(expectedText, StringComparison.Ordinal))
                return;

            if (_process?.HasExited == true)
                break;

            await Task.Delay(50, _cancellation.Token);
        }

        throw new TimeoutException(
            $"Timed out waiting for output containing \"{expectedText}\".{Environment.NewLine}{GetTranscript()}");
    }

    public async Task<int> WaitForExitAsync(TimeSpan? timeout = null)
    {
        if (_process == null)
            throw new InvalidOperationException("Process was not started.");

        using var timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(20));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, timeoutCts.Token);
        await _process.WaitForExitAsync(linkedCts.Token);

        if (_standardOutputPump != null)
            await _standardOutputPump;
        if (_standardErrorPump != null)
            await _standardErrorPump;

        return _process.ExitCode;
    }

    public string GetTranscript()
    {
        lock (_transcriptLock)
        {
            if (_standardError.Length == 0)
                return _standardOutput.ToString();

            return $"{_standardOutput}{Environment.NewLine}[stderr]{Environment.NewLine}{_standardError}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        await _testHostClient.DisposeAsync();

        if (_process != null)
        {
            if (!_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            }

            _process.Dispose();
        }

        if (_standardOutputPump != null)
            await SafeAwaitAsync(_standardOutputPump);
        if (_standardErrorPump != null)
            await SafeAwaitAsync(_standardErrorPump);

        _cancellation.Dispose();
    }

    private Task PumpStreamAsync(StreamReader reader, StringBuilder target, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            var buffer = new char[256];
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read <= 0)
                    break;

                lock (_transcriptLock)
                {
                    target.Append(buffer, 0, read);
                }
            }
        }, cancellationToken);
    }

    private static async Task SafeAwaitAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }
}
