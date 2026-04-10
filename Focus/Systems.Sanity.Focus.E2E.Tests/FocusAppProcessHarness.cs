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
    private const string ClipboardModeEnvironmentVariable = "FOCUS_TEST_CLIPBOARD_MODE";
    private const string ClipboardTextBase64EnvironmentVariable = "FOCUS_TEST_CLIPBOARD_TEXT_BASE64";
    private const string ClipboardImageBase64EnvironmentVariable = "FOCUS_TEST_CLIPBOARD_IMAGE_BASE64";
    private const string ClipboardErrorEnvironmentVariable = "FOCUS_TEST_CLIPBOARD_ERROR";
    private const string OpenedFilesLogPathEnvironmentVariable = "FOCUS_TEST_OPENED_FILES_LOG";
    private const string EmitTitlesEnvironmentVariable = "FOCUS_TEST_EMIT_TITLES";

    private readonly FocusE2EWorkspace _workspace;
    private readonly FocusAppLaunchOptions _launchOptions;
    private readonly FocusTestHostClient _testHostClient;
    private readonly string _pipeName;
    private readonly StringBuilder _standardOutput = new();
    private readonly StringBuilder _standardError = new();
    private readonly object _transcriptLock = new();
    private readonly CancellationTokenSource _cancellation = new();

    private Process? _process;
    private Task? _standardOutputPump;
    private Task? _standardErrorPump;

    public FocusAppProcessHarness(FocusE2EWorkspace workspace, FocusAppLaunchOptions? launchOptions = null)
    {
        _workspace = workspace;
        _launchOptions = launchOptions ?? new FocusAppLaunchOptions();
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
        startInfo.Environment[OpenedFilesLogPathEnvironmentVariable] = _workspace.OpenedFilesLogPath;
        ApplyClipboardOverrides(startInfo);
        if (_launchOptions.EmitTitles)
            startInfo.Environment[EmitTitlesEnvironmentVariable] = "1";

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

    public async Task WaitForOutputOccurrencesAsync(string expectedText, int occurrences, TimeSpan? timeout = null)
    {
        if (occurrences <= 0)
            throw new ArgumentOutOfRangeException(nameof(occurrences), "Occurrences must be greater than zero.");

        var deadlineUtc = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow <= deadlineUtc)
        {
            if (CountOccurrences(GetTranscript(), expectedText) >= occurrences)
                return;

            if (_process?.HasExited == true)
                break;

            await Task.Delay(50, _cancellation.Token);
        }

        throw new TimeoutException(
            $"Timed out waiting for output containing \"{expectedText}\" at least {occurrences} times.{Environment.NewLine}{GetTranscript()}");
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

    private void ApplyClipboardOverrides(ProcessStartInfo startInfo)
    {
        var modesSpecified = 0;
        if (_launchOptions.ClipboardText != null)
            modesSpecified++;
        if (_launchOptions.ClipboardImageBytes != null)
            modesSpecified++;
        if (_launchOptions.ClipboardErrorMessage != null)
            modesSpecified++;
        if (_launchOptions.ClipboardExceptionMessage != null)
            modesSpecified++;

        if (modesSpecified > 1)
            throw new InvalidOperationException("Specify only one clipboard override per app launch.");

        if (_launchOptions.ClipboardText != null)
        {
            startInfo.Environment[ClipboardModeEnvironmentVariable] = "text";
            startInfo.Environment[ClipboardTextBase64EnvironmentVariable] =
                Convert.ToBase64String(Encoding.UTF8.GetBytes(_launchOptions.ClipboardText));
            return;
        }

        if (_launchOptions.ClipboardImageBytes != null)
        {
            startInfo.Environment[ClipboardModeEnvironmentVariable] = "image";
            startInfo.Environment[ClipboardImageBase64EnvironmentVariable] =
                Convert.ToBase64String(_launchOptions.ClipboardImageBytes);
            return;
        }

        if (_launchOptions.ClipboardErrorMessage != null)
        {
            startInfo.Environment[ClipboardModeEnvironmentVariable] = "error";
            startInfo.Environment[ClipboardErrorEnvironmentVariable] = _launchOptions.ClipboardErrorMessage;
            return;
        }

        if (_launchOptions.ClipboardExceptionMessage != null)
        {
            startInfo.Environment[ClipboardModeEnvironmentVariable] = "throw";
            startInfo.Environment[ClipboardErrorEnvironmentVariable] = _launchOptions.ClipboardExceptionMessage;
        }
    }

    private static int CountOccurrences(string content, string expectedText)
    {
        if (string.IsNullOrEmpty(expectedText))
            return 0;

        var count = 0;
        var startIndex = 0;
        while (true)
        {
            var index = content.IndexOf(expectedText, startIndex, StringComparison.Ordinal);
            if (index < 0)
                return count;

            count++;
            startIndex = index + expectedText.Length;
        }
    }
}
