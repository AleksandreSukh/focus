#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Application;

internal interface IVoiceRecorder
{
    VoiceRecordingStartResult Start(VoiceRecordingOptions options);

    Task<VoiceRecordingResult> StopAsync();

    Task<VoiceRecordingCancelResult> CancelAsync();
}

internal sealed record VoiceRecordingOptions(TimeSpan MaxDuration);

internal sealed record VoiceRecordingStartResult(string? ErrorMessage = null)
{
    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);

    public static VoiceRecordingStartResult Success() => new();

    public static VoiceRecordingStartResult Error(string errorMessage) => new(errorMessage);
}

internal sealed record VoiceRecordingResult(
    string? FilePath,
    string FileExtension,
    string MediaType,
    string? ErrorMessage = null)
{
    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage) && !string.IsNullOrWhiteSpace(FilePath);

    public static VoiceRecordingResult Success(string filePath, string fileExtension, string mediaType) =>
        new(filePath, fileExtension, mediaType);

    public static VoiceRecordingResult Error(string errorMessage) =>
        new(null, string.Empty, string.Empty, errorMessage);
}

internal sealed record VoiceRecordingCancelResult(string? ErrorMessage = null)
{
    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);

    public static VoiceRecordingCancelResult Success() => new();

    public static VoiceRecordingCancelResult Error(string errorMessage) => new(errorMessage);
}

internal static class VoiceRecorderFactory
{
    public static IVoiceRecorder CreateDefault(
        VoiceRecorderConfig? config = null,
        IVoiceRecorderProcessRunner? processRunner = null,
        string? bundledToolsBaseDirectory = null)
    {
        processRunner ??= new ProcessVoiceRecorderProcessRunner();
        if (!DefaultVoiceRecorderArguments.IsSupportedOperatingSystem &&
            (config?.Arguments == null || config.Arguments.Length == 0))
        {
            return new UnsupportedVoiceRecorder("Voice recording is not supported on this operating system.");
        }

        var settings = VoiceRecorderSettings.FromConfig(
            config,
            BundledFfmpegResolver.ResolveDefaultCommand(bundledToolsBaseDirectory));
        return new ExternalVoiceRecorder(settings, processRunner);
    }
}

internal sealed record VoiceRecorderSettings(
    string Command,
    IReadOnlyList<string> Arguments,
    string FileExtension,
    string MediaType,
    bool UsesConfiguredArguments)
{
    public static VoiceRecorderSettings FromConfig(
        VoiceRecorderConfig? config,
        string defaultCommand)
    {
        var command = string.IsNullOrWhiteSpace(config?.Command)
            ? defaultCommand
            : config.Command.Trim();
        var usesConfiguredArguments = config?.Arguments is { Length: > 0 };
        var arguments = usesConfiguredArguments
            ? config!.Arguments.Where(argument => argument != null).ToArray()
            : Array.Empty<string>();
        var fileExtension = NormalizeFileExtension(config?.FileExtension, ".webm");
        var mediaType = string.IsNullOrWhiteSpace(config?.MediaType)
            ? "audio/webm; codecs=opus"
            : config.MediaType.Trim();

        return new VoiceRecorderSettings(command, arguments, fileExtension, mediaType, usesConfiguredArguments);
    }

    private static string NormalizeFileExtension(string? value, string fallback)
    {
        var extension = string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
        return extension.StartsWith(".", StringComparison.Ordinal)
            ? extension
            : $".{extension}";
    }
}

internal sealed record VoiceRecorderDefaultArgumentsResult(
    IReadOnlyList<string> Arguments,
    string? ErrorMessage = null)
{
    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);

    public static VoiceRecorderDefaultArgumentsResult Success(IReadOnlyList<string> arguments) =>
        new(arguments);

    public static VoiceRecorderDefaultArgumentsResult Error(string errorMessage) =>
        new(Array.Empty<string>(), errorMessage);
}

internal static class DefaultVoiceRecorderArguments
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public static bool IsSupportedOperatingSystem =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

    public static VoiceRecorderDefaultArgumentsResult Build(
        string command,
        IVoiceRecorderProcessRunner processRunner)
    {
        if (OperatingSystem.IsWindows())
            return BuildWindows(command, processRunner);

        if (OperatingSystem.IsMacOS())
            return VoiceRecorderDefaultArgumentsResult.Success(
                BuildCommonArguments(new[] { "-f", "avfoundation", "-i", ":0" }));

        if (OperatingSystem.IsLinux())
            return VoiceRecorderDefaultArgumentsResult.Success(
                BuildCommonArguments(new[] { "-f", "pulse", "-i", "default" }));

        return VoiceRecorderDefaultArgumentsResult.Error("Voice recording is not supported on this operating system.");
    }

    internal static VoiceRecorderDefaultArgumentsResult BuildWindows(
        string command,
        IVoiceRecorderProcessRunner processRunner)
    {
        var wasapiProbe = ProbeInputFormat(command, processRunner, "wasapi");
        if (!string.IsNullOrWhiteSpace(wasapiProbe.ErrorMessage))
            return VoiceRecorderDefaultArgumentsResult.Error(wasapiProbe.ErrorMessage);

        if (wasapiProbe.IsSupported)
        {
            return VoiceRecorderDefaultArgumentsResult.Success(
                BuildCommonArguments(new[] { "-f", "wasapi", "-i", "default" }));
        }

        var dshowProbe = ProbeInputFormat(command, processRunner, "dshow");
        if (!string.IsNullOrWhiteSpace(dshowProbe.ErrorMessage))
            return VoiceRecorderDefaultArgumentsResult.Error(dshowProbe.ErrorMessage);

        if (!dshowProbe.IsSupported)
        {
            return VoiceRecorderDefaultArgumentsResult.Error(
                "Voice recording could not find a supported Windows ffmpeg audio input. " +
                "The selected ffmpeg does not report WASAPI or DirectShow support. " +
                "Use a full ffmpeg build or configure voiceRecorder.command and voiceRecorder.arguments.");
        }

        var dshowDevices = processRunner.Run(
            command,
            new[] { "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy" },
            ProbeTimeout);
        if (dshowDevices.Exception != null)
            return VoiceRecorderDefaultArgumentsResult.Error(BuildProbeFailureMessage(dshowDevices.Exception));

        var deviceName = ParseFirstDirectShowAudioDevice(dshowDevices.CombinedOutput);
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return VoiceRecorderDefaultArgumentsResult.Error(
                "Voice recording could not find a DirectShow audio input device. " +
                "Run `ffmpeg -hide_banner -list_devices true -f dshow -i dummy` to inspect devices, " +
                "or configure voiceRecorder.arguments with the microphone device to use.");
        }

        return VoiceRecorderDefaultArgumentsResult.Success(
            BuildCommonArguments(new[] { "-f", "dshow", "-i", $"audio={deviceName}" }));
    }

    internal static string? ParseFirstDirectShowAudioDevice(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var match = Regex.Match(
            output,
            "\"(?<name>[^\"]+)\"\\s+\\(audio\\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success
            ? match.Groups["name"].Value
            : null;
    }

    private static VoiceRecorderFormatProbeResult ProbeInputFormat(
        string command,
        IVoiceRecorderProcessRunner processRunner,
        string formatName)
    {
        var devicesResult = processRunner.Run(
            command,
            new[] { "-hide_banner", "-devices" },
            ProbeTimeout);
        if (devicesResult.Exception != null)
            return VoiceRecorderFormatProbeResult.Error(BuildProbeFailureMessage(devicesResult.Exception));

        if (OutputListsInputFormat(devicesResult.CombinedOutput, formatName))
            return VoiceRecorderFormatProbeResult.Supported();

        var formatsResult = processRunner.Run(
            command,
            new[] { "-hide_banner", "-formats" },
            ProbeTimeout);
        if (formatsResult.Exception != null)
            return VoiceRecorderFormatProbeResult.Error(BuildProbeFailureMessage(formatsResult.Exception));

        return OutputListsInputFormat(formatsResult.CombinedOutput, formatName)
            ? VoiceRecorderFormatProbeResult.Supported()
            : VoiceRecorderFormatProbeResult.Unsupported();
    }

    private static bool OutputListsInputFormat(string output, string formatName) =>
        Regex.IsMatch(
            output ?? string.Empty,
            $@"(?im)^\s*D\S*\s+{Regex.Escape(formatName)}\s",
            RegexOptions.CultureInvariant);

    private static IReadOnlyList<string> BuildCommonArguments(IReadOnlyList<string> inputArguments) =>
        new[]
        {
            "-hide_banner",
            "-loglevel",
            "error"
        }
            .Concat(inputArguments)
            .Concat(new[]
            {
                "-t",
                "{seconds}",
                "-vn",
                "-c:a",
                "libopus",
                "-b:a",
                "64k",
                "-y",
                "{output}"
            })
            .ToArray();

    private static string BuildProbeFailureMessage(Exception exception) =>
        "Voice recording requires the bundled ffmpeg executable or a configured voiceRecorder.command. " +
        exception.Message;

    private sealed record VoiceRecorderFormatProbeResult(
        bool IsSupported,
        string? ErrorMessage = null)
    {
        public static VoiceRecorderFormatProbeResult Supported() => new(true);

        public static VoiceRecorderFormatProbeResult Unsupported() => new(false);

        public static VoiceRecorderFormatProbeResult Error(string errorMessage) => new(false, errorMessage);
    }
}

internal static class BundledFfmpegResolver
{
    public const string RelativeToolsDirectory = "Tools";
    public const string RelativeFfmpegDirectory = "ffmpeg";

    public static string ResolveDefaultCommand(string? baseDirectory = null)
    {
        foreach (var candidatePath in EnumerateCandidatePaths(baseDirectory ?? AppContext.BaseDirectory))
        {
            if (File.Exists(candidatePath))
                return candidatePath;
        }

        return GetExecutableFileName();
    }

    public static IReadOnlyList<string> EnumerateCandidatePaths(string baseDirectory)
    {
        var normalizedBaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : baseDirectory;
        var executableName = GetExecutableFileName();
        var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
        var platformIdentifier = GetPlatformIdentifier();
        var candidates = new List<string>
        {
            Path.Combine(normalizedBaseDirectory, executableName),
            Path.Combine(normalizedBaseDirectory, RelativeToolsDirectory, RelativeFfmpegDirectory, executableName),
            Path.Combine(normalizedBaseDirectory, RelativeToolsDirectory, RelativeFfmpegDirectory, runtimeIdentifier, executableName),
            Path.Combine(normalizedBaseDirectory, RelativeToolsDirectory, RelativeFfmpegDirectory, platformIdentifier, executableName),
            Path.Combine(normalizedBaseDirectory, "runtimes", runtimeIdentifier, "native", executableName)
        };

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetExecutableFileName() =>
        OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    private static string GetPlatformIdentifier()
    {
        if (OperatingSystem.IsWindows())
            return "win-x64";

        if (OperatingSystem.IsMacOS())
            return "osx-arm64";

        if (OperatingSystem.IsLinux())
            return "linux-x64";

        return RuntimeInformation.RuntimeIdentifier;
    }
}

internal interface IVoiceRecorderProcessRunner
{
    VoiceRecorderCommandResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout);

    VoiceRecorderProcessStartResult Start(string fileName, IReadOnlyList<string> arguments);
}

internal sealed record VoiceRecorderCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    Exception? Exception = null)
{
    public bool IsSuccess => Exception == null && ExitCode == 0;

    public string CombinedOutput => $"{StandardOutput}{Environment.NewLine}{StandardError}";
}

internal sealed record VoiceRecorderProcessStartResult(
    IVoiceRecorderProcess? Process,
    Exception? StartException = null,
    VoiceRecorderProcessExitResult? ImmediateExit = null)
{
    public bool IsSuccess => Process != null && StartException == null && ImmediateExit == null;

    public static VoiceRecorderProcessStartResult Success(IVoiceRecorderProcess process) => new(process);

    public static VoiceRecorderProcessStartResult Error(Exception exception) => new(null, exception);

    public static VoiceRecorderProcessStartResult Exited(VoiceRecorderProcessExitResult exitResult) =>
        new(null, null, exitResult);
}

internal interface IVoiceRecorderProcess
{
    Task<VoiceRecorderProcessExitResult> StopAsync(TimeSpan timeout);
}

internal sealed record VoiceRecorderProcessExitResult(
    int ExitCode,
    string StandardError,
    Exception? Exception = null)
{
    public bool IsSuccess => Exception == null && ExitCode == 0;
}

internal sealed class ProcessVoiceRecorderProcessRunner : IVoiceRecorderProcessRunner
{
    private static readonly TimeSpan ImmediateExitProbeTimeout = TimeSpan.FromMilliseconds(500);

    public VoiceRecorderCommandResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = BuildProcessStartInfo(fileName, arguments, redirectStandardInput: false, redirectStandardOutput: true)
            };

            process.Start();
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)Math.Ceiling(timeout.TotalMilliseconds)))
            {
                TryKill(process);
                process.WaitForExit();
                var timedOutStandardError = standardErrorTask.GetAwaiter().GetResult();
                return new VoiceRecorderCommandResult(
                    -1,
                    standardOutputTask.GetAwaiter().GetResult(),
                    string.IsNullOrWhiteSpace(timedOutStandardError)
                        ? "Voice recorder command did not finish within the expected timeout."
                        : timedOutStandardError);
            }

            return new VoiceRecorderCommandResult(
                process.ExitCode,
                standardOutputTask.GetAwaiter().GetResult(),
                standardErrorTask.GetAwaiter().GetResult());
        }
        catch (Exception ex)
        {
            return new VoiceRecorderCommandResult(-1, string.Empty, string.Empty, ex);
        }
    }

    public VoiceRecorderProcessStartResult Start(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = BuildProcessStartInfo(fileName, arguments, redirectStandardInput: true, redirectStandardOutput: false)
            };

            process.Start();
            if (process.WaitForExit((int)Math.Ceiling(ImmediateExitProbeTimeout.TotalMilliseconds)))
            {
                var standardError = process.StandardError.ReadToEnd();
                var exitResult = new VoiceRecorderProcessExitResult(process.ExitCode, standardError);
                process.Dispose();
                return VoiceRecorderProcessStartResult.Exited(exitResult);
            }

            return VoiceRecorderProcessStartResult.Success(new VoiceRecorderProcess(process));
        }
        catch (Exception ex)
        {
            return VoiceRecorderProcessStartResult.Error(ex);
        }
    }

    private static ProcessStartInfo BuildProcessStartInfo(
        string fileName,
        IReadOnlyList<string> arguments,
        bool redirectStandardInput,
        bool redirectStandardOutput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = redirectStandardOutput,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }
}

internal sealed class VoiceRecorderProcess : IVoiceRecorderProcess
{
    private readonly Process _process;
    private readonly Task<string> _standardErrorTask;

    public VoiceRecorderProcess(Process process)
    {
        _process = process;
        _standardErrorTask = process.StandardError.ReadToEndAsync();
    }

    public async Task<VoiceRecorderProcessExitResult> StopAsync(TimeSpan timeout)
    {
        try
        {
            TryRequestGracefulStop();

            using var timeoutCts = new CancellationTokenSource(timeout);
            try
            {
                await _process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill();
                await _process.WaitForExitAsync();
                return new VoiceRecorderProcessExitResult(
                    -1,
                    "Voice recorder did not stop within the expected timeout.");
            }

            return new VoiceRecorderProcessExitResult(
                _process.ExitCode,
                await _standardErrorTask);
        }
        catch (Exception ex)
        {
            return new VoiceRecorderProcessExitResult(-1, string.Empty, ex);
        }
        finally
        {
            _process.Dispose();
        }
    }

    private void TryRequestGracefulStop()
    {
        try
        {
            if (_process.HasExited)
                return;

            _process.StandardInput.Write("q");
            _process.StandardInput.Flush();
        }
        catch
        {
            // If stdin has already closed, the process is either exiting or will
            // be handled by the timeout path.
        }
    }

    private void TryKill()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }
}

internal sealed class ExternalVoiceRecorder : IVoiceRecorder
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private readonly VoiceRecorderSettings _settings;
    private readonly IVoiceRecorderProcessRunner _processRunner;
    private ActiveVoiceRecording? _activeRecording;

    public ExternalVoiceRecorder(VoiceRecorderSettings settings, IVoiceRecorderProcessRunner processRunner)
    {
        _settings = settings;
        _processRunner = processRunner;
    }

    public VoiceRecordingStartResult Start(VoiceRecordingOptions options)
    {
        if (_activeRecording != null)
            return VoiceRecordingStartResult.Error("A voice recording is already in progress.");

        var argumentsResult = ResolveArguments();
        if (!argumentsResult.IsSuccess)
            return VoiceRecordingStartResult.Error(argumentsResult.ErrorMessage!);

        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{_settings.FileExtension}");
        var arguments = BuildArguments(argumentsResult.Arguments, outputPath, options.MaxDuration);
        var startResult = _processRunner.Start(_settings.Command, arguments);
        if (startResult.ImmediateExit != null)
        {
            DeleteFileIfExists(outputPath);
            return VoiceRecordingStartResult.Error(
                BuildCommandFailureMessage(startResult.ImmediateExit, _settings.UsesConfiguredArguments));
        }

        if (!startResult.IsSuccess || startResult.Process == null)
        {
            DeleteFileIfExists(outputPath);
            return VoiceRecordingStartResult.Error(BuildStartFailureMessage(startResult.StartException));
        }

        _activeRecording = new ActiveVoiceRecording(
            startResult.Process,
            outputPath,
            _settings.FileExtension,
            _settings.MediaType);
        return VoiceRecordingStartResult.Success();
    }

    public async Task<VoiceRecordingResult> StopAsync()
    {
        var activeRecording = TakeActiveRecording();
        if (activeRecording == null)
            return VoiceRecordingResult.Error("No active voice recording is in progress.");

        var exitResult = await activeRecording.Process.StopAsync(StopTimeout);
        if (!exitResult.IsSuccess)
        {
            DeleteFileIfExists(activeRecording.OutputFilePath);
            return VoiceRecordingResult.Error(
                BuildCommandFailureMessage(exitResult, _settings.UsesConfiguredArguments));
        }

        if (!File.Exists(activeRecording.OutputFilePath))
            return VoiceRecordingResult.Error("Voice recording did not produce an audio file.");

        if (new FileInfo(activeRecording.OutputFilePath).Length == 0)
        {
            DeleteFileIfExists(activeRecording.OutputFilePath);
            return VoiceRecordingResult.Error("Voice recording produced an empty audio file.");
        }

        return VoiceRecordingResult.Success(
            activeRecording.OutputFilePath,
            activeRecording.FileExtension,
            activeRecording.MediaType);
    }

    public async Task<VoiceRecordingCancelResult> CancelAsync()
    {
        var activeRecording = TakeActiveRecording();
        if (activeRecording == null)
            return VoiceRecordingCancelResult.Success();

        var exitResult = await activeRecording.Process.StopAsync(StopTimeout);
        DeleteFileIfExists(activeRecording.OutputFilePath);
        return exitResult.Exception == null
            ? VoiceRecordingCancelResult.Success()
            : VoiceRecordingCancelResult.Error("Couldn't cancel the voice recording cleanly.");
    }

    private VoiceRecorderDefaultArgumentsResult ResolveArguments() =>
        _settings.UsesConfiguredArguments
            ? VoiceRecorderDefaultArgumentsResult.Success(_settings.Arguments)
            : DefaultVoiceRecorderArguments.Build(_settings.Command, _processRunner);

    private static IReadOnlyList<string> BuildArguments(
        IReadOnlyList<string> argumentTemplates,
        string outputPath,
        TimeSpan maxDuration)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(maxDuration.TotalSeconds));
        return argumentTemplates
            .Select(argument => argument
                .Replace("{output}", outputPath, StringComparison.Ordinal)
                .Replace("{seconds}", seconds.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            .ToArray();
    }

    private ActiveVoiceRecording? TakeActiveRecording()
    {
        var activeRecording = _activeRecording;
        _activeRecording = null;
        return activeRecording;
    }

    private static string BuildStartFailureMessage(Exception? exception)
    {
        var detail = exception?.Message;
        return string.IsNullOrWhiteSpace(detail)
            ? "Voice recording requires the bundled ffmpeg executable. Add it under Tools/ffmpeg for this platform or configure voiceRecorder.command and voiceRecorder.arguments."
            : $"Voice recording requires the bundled ffmpeg executable or a configured voiceRecorder.command. {detail}";
    }

    private static string BuildCommandFailureMessage(
        VoiceRecorderProcessExitResult result,
        bool usesConfiguredArguments)
    {
        if (result.Exception != null)
            return $"Voice recording failed: {result.Exception.Message}";

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            var standardError = result.StandardError.Trim();
            if (usesConfiguredArguments && IsUnsupportedWasapiError(standardError))
            {
                return "Voice recording failed because the configured ffmpeg does not support WASAPI. " +
                    "Remove the voiceRecorder block from focus-config.json to use the app default, " +
                    "or configure DirectShow arguments such as -f dshow -i audio=<device name>. " +
                    $"ffmpeg said: {standardError}";
            }

            return $"Voice recording failed: {standardError}";
        }

        return "Voice recording failed. Check microphone access, the bundled ffmpeg executable, or configure voiceRecorder.command and voiceRecorder.arguments.";
    }

    private static bool IsUnsupportedWasapiError(string standardError) =>
        standardError.Contains("Unknown input format", StringComparison.OrdinalIgnoreCase) &&
        standardError.Contains("wasapi", StringComparison.OrdinalIgnoreCase);

    private static void DeleteFileIfExists(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    private sealed record ActiveVoiceRecording(
        IVoiceRecorderProcess Process,
        string OutputFilePath,
        string FileExtension,
        string MediaType);
}

internal sealed class UnsupportedVoiceRecorder : IVoiceRecorder
{
    private readonly string _errorMessage;

    public UnsupportedVoiceRecorder(string errorMessage)
    {
        _errorMessage = errorMessage;
    }

    public VoiceRecordingStartResult Start(VoiceRecordingOptions options) =>
        VoiceRecordingStartResult.Error(_errorMessage);

    public Task<VoiceRecordingResult> StopAsync() =>
        Task.FromResult(VoiceRecordingResult.Error(_errorMessage));

    public Task<VoiceRecordingCancelResult> CancelAsync() =>
        Task.FromResult(VoiceRecordingCancelResult.Success());
}
