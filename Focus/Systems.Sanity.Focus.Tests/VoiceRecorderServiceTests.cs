using System.Text;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Tests;

public class VoiceRecorderServiceTests
{
    [Fact]
    public async Task ExternalRecorder_UsesConfiguredCommandArgumentsAndMetadata()
    {
        var runner = new RecordingVoiceRecorderProcessRunner();
        runner.NextProcessFactory = call => new StubVoiceRecorderProcess(async () =>
        {
            var outputPath = call.Arguments.Single(argument => argument.EndsWith(".ogg", StringComparison.Ordinal));
            await File.WriteAllBytesAsync(outputPath, Encoding.UTF8.GetBytes("voice"));
            return new VoiceRecorderProcessExitResult(0, string.Empty);
        });
        var recorder = VoiceRecorderFactory.CreateDefault(
            new VoiceRecorderConfig
            {
                Command = "custom-recorder",
                Arguments = new[] { "--duration", "{seconds}", "--out", "{output}" },
                FileExtension = "ogg",
                MediaType = "audio/ogg"
            },
            runner);

        var startResult = recorder.Start(new VoiceRecordingOptions(TimeSpan.FromSeconds(42)));
        var stopResult = await recorder.StopAsync();

        Assert.True(startResult.IsSuccess);
        Assert.True(stopResult.IsSuccess);
        Assert.Equal("custom-recorder", runner.Calls.Single().FileName);
        Assert.Equal(["--duration", "42", "--out"], runner.Calls.Single().Arguments.Take(3));
        Assert.EndsWith(".ogg", runner.Calls.Single().Arguments.Last());
        Assert.Equal(".ogg", stopResult.FileExtension);
        Assert.Equal("audio/ogg", stopResult.MediaType);
        Assert.True(File.Exists(stopResult.FilePath));
        File.Delete(stopResult.FilePath!);
    }

    [Fact]
    public async Task DefaultRecorder_UsesBundledFfmpegBeforePath()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "focus-tests", Guid.NewGuid().ToString("N"));
        var bundledFfmpegPath = BundledFfmpegResolver
            .EnumerateCandidatePaths(baseDirectory)
            .First(path => path.Contains(
                Path.Combine(BundledFfmpegResolver.RelativeToolsDirectory, BundledFfmpegResolver.RelativeFfmpegDirectory),
                StringComparison.OrdinalIgnoreCase));
        Directory.CreateDirectory(Path.GetDirectoryName(bundledFfmpegPath)!);
        File.WriteAllText(bundledFfmpegPath, string.Empty);
        var runner = new RecordingVoiceRecorderProcessRunner();
        if (OperatingSystem.IsWindows())
        {
            runner.EnqueueCommandResult(new VoiceRecorderCommandResult(
                0,
                " D  wasapi         Windows audio session API",
                string.Empty));
        }

        runner.NextProcessFactory = call => new StubVoiceRecorderProcess(async () =>
        {
            var outputPath = call.Arguments.Single(argument => argument.EndsWith(".webm", StringComparison.Ordinal));
            await File.WriteAllBytesAsync(outputPath, Encoding.UTF8.GetBytes("voice"));
            return new VoiceRecorderProcessExitResult(0, string.Empty);
        });

        try
        {
            var recorder = VoiceRecorderFactory.CreateDefault(
                processRunner: runner,
                bundledToolsBaseDirectory: baseDirectory);

            var startResult = recorder.Start(new VoiceRecordingOptions(TimeSpan.FromSeconds(10)));
            var stopResult = await recorder.StopAsync();

            Assert.True(startResult.IsSuccess);
            Assert.True(stopResult.IsSuccess);
            Assert.Equal(bundledFfmpegPath, runner.Calls.Single().FileName);
            Assert.Contains("-c:a", runner.Calls.Single().Arguments);
            File.Delete(stopResult.FilePath!);
        }
        finally
        {
            if (Directory.Exists(baseDirectory))
                Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DefaultWindowsRecorder_UsesWasapiWhenSelectedFfmpegSupportsIt()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var runner = new RecordingVoiceRecorderProcessRunner();
        runner.EnqueueCommandResult(new VoiceRecorderCommandResult(
            0,
            " D  wasapi         Windows audio session API",
            string.Empty));
        var recorder = new ExternalVoiceRecorder(
            new VoiceRecorderSettings(
                "ffmpeg",
                Array.Empty<string>(),
                ".webm",
                "audio/webm; codecs=opus",
                UsesConfiguredArguments: false),
            runner);

        var startResult = recorder.Start(new VoiceRecordingOptions(TimeSpan.FromSeconds(10)));
        var cancelResult = await recorder.CancelAsync();

        Assert.True(startResult.IsSuccess);
        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(["-f", "wasapi", "-i", "default"], runner.Calls.Single().Arguments.Skip(3).Take(4));
    }

    [Fact]
    public async Task DefaultWindowsRecorder_FallsBackToFirstDirectShowAudioDevice()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var runner = new RecordingVoiceRecorderProcessRunner();
        runner.EnqueueCommandResult(new VoiceRecorderCommandResult(
            0,
            " D  dshow           DirectShow capture",
            string.Empty));
        runner.EnqueueCommandResult(new VoiceRecorderCommandResult(
            0,
            string.Empty,
            string.Empty));
        runner.EnqueueCommandResult(new VoiceRecorderCommandResult(
            0,
            " D  dshow           DirectShow capture",
            string.Empty));
        runner.EnqueueCommandResult(new VoiceRecorderCommandResult(
            1,
            string.Empty,
            "[in#0] \"Microphone Array (Realtek(R) Audio)\" (audio)" + Environment.NewLine +
            "[in#0]   Alternative name \"@device_cm_{id}\""));
        var recorder = new ExternalVoiceRecorder(
            new VoiceRecorderSettings(
                "ffmpeg",
                Array.Empty<string>(),
                ".webm",
                "audio/webm; codecs=opus",
                UsesConfiguredArguments: false),
            runner);

        var startResult = recorder.Start(new VoiceRecordingOptions(TimeSpan.FromSeconds(10)));
        var cancelResult = await recorder.CancelAsync();

        Assert.True(startResult.IsSuccess);
        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(
            ["-f", "dshow", "-i", "audio=Microphone Array (Realtek(R) Audio)"],
            runner.Calls.Single().Arguments.Skip(3).Take(4));
    }

    [Fact]
    public void DefaultWindowsRecorder_ReturnsClearErrorWhenNoSupportedInputExists()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var runner = new RecordingVoiceRecorderProcessRunner();
        var recorder = new ExternalVoiceRecorder(
            new VoiceRecorderSettings(
                "ffmpeg",
                Array.Empty<string>(),
                ".webm",
                "audio/webm; codecs=opus",
                UsesConfiguredArguments: false),
            runner);

        var result = recorder.Start(new VoiceRecordingOptions(TimeSpan.FromSeconds(10)));

        Assert.False(result.IsSuccess);
        Assert.Contains("supported Windows ffmpeg audio input", result.ErrorMessage);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void BundledFfmpegResolver_FallsBackToPathCommandWhenBundledBinaryIsMissing()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "focus-tests", Guid.NewGuid().ToString("N"));

        var command = BundledFfmpegResolver.ResolveDefaultCommand(baseDirectory);

        Assert.Equal(OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg", command);
    }

    [Fact]
    public void ExternalRecorder_StartFailure_ReturnsSetupGuidance()
    {
        var runner = new RecordingVoiceRecorderProcessRunner
        {
            StartException = new FileNotFoundException("not found")
        };
        var recorder = VoiceRecorderFactory.CreateDefault(
            new VoiceRecorderConfig
            {
                Arguments = new[] { "{output}" }
            },
            runner);

        var result = recorder.Start(new VoiceRecordingOptions(TimeSpan.FromSeconds(10)));

        Assert.False(result.IsSuccess);
        Assert.Contains("voiceRecorder.command", result.ErrorMessage);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public void ExternalRecorder_ConfiguredWasapiImmediateFailure_ReturnsSpecificGuidance()
    {
        var runner = new RecordingVoiceRecorderProcessRunner
        {
            ImmediateExitResult = new VoiceRecorderProcessExitResult(
                1,
                "[in#0] Unknown input format: 'wasapi'")
        };
        var recorder = VoiceRecorderFactory.CreateDefault(
            new VoiceRecorderConfig
            {
                Command = "custom-ffmpeg",
                Arguments = new[] { "-f", "wasapi", "-i", "default", "{output}" }
            },
            runner);

        var result = recorder.Start(new VoiceRecordingOptions(TimeSpan.FromSeconds(10)));

        Assert.False(result.IsSuccess);
        Assert.Contains("configured ffmpeg does not support WASAPI", result.ErrorMessage);
        Assert.Contains("Remove the voiceRecorder block", result.ErrorMessage);
    }

    [Fact]
    public void ExternalRecorder_ImmediateExit_ReturnsStartError()
    {
        var runner = new RecordingVoiceRecorderProcessRunner
        {
            ImmediateExitResult = new VoiceRecorderProcessExitResult(1, "microphone not found")
        };
        var recorder = VoiceRecorderFactory.CreateDefault(
            new VoiceRecorderConfig
            {
                Arguments = new[] { "{output}" }
            },
            runner);

        var result = recorder.Start(new VoiceRecordingOptions(TimeSpan.FromSeconds(10)));

        Assert.False(result.IsSuccess);
        Assert.Equal("Voice recording failed: microphone not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExternalRecorder_StopFailure_ReturnsRecorderError()
    {
        var runner = new RecordingVoiceRecorderProcessRunner
        {
            NextProcessFactory = _ => new StubVoiceRecorderProcess(() =>
                Task.FromResult(new VoiceRecorderProcessExitResult(1, "microphone not found")))
        };
        var recorder = VoiceRecorderFactory.CreateDefault(
            new VoiceRecorderConfig
            {
                Arguments = new[] { "{output}" }
            },
            runner);

        var startResult = recorder.Start(new VoiceRecordingOptions(TimeSpan.FromSeconds(10)));
        var stopResult = await recorder.StopAsync();

        Assert.True(startResult.IsSuccess);
        Assert.False(stopResult.IsSuccess);
        Assert.Equal("Voice recording failed: microphone not found", stopResult.ErrorMessage);
    }

    [Fact]
    public async Task ExternalRecorder_CancelDeletesPartialFile()
    {
        var runner = new RecordingVoiceRecorderProcessRunner();
        string? outputPath = null;
        runner.NextProcessFactory = call =>
        {
            outputPath = call.Arguments.Single(argument => argument.EndsWith(".webm", StringComparison.Ordinal));
            File.WriteAllText(outputPath, "partial");
            return new StubVoiceRecorderProcess(() =>
                Task.FromResult(new VoiceRecorderProcessExitResult(0, string.Empty)));
        };
        var recorder = VoiceRecorderFactory.CreateDefault(
            new VoiceRecorderConfig
            {
                Arguments = new[] { "{output}" }
            },
            runner);

        var startResult = recorder.Start(new VoiceRecordingOptions(TimeSpan.FromSeconds(10)));
        var cancelResult = await recorder.CancelAsync();

        Assert.True(startResult.IsSuccess);
        Assert.True(cancelResult.IsSuccess);
        Assert.NotNull(outputPath);
        Assert.False(File.Exists(outputPath));
    }

    private sealed class RecordingVoiceRecorderProcessRunner : IVoiceRecorderProcessRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = new();

        public List<(string FileName, IReadOnlyList<string> Arguments)> CommandCalls { get; } = new();

        public Queue<VoiceRecorderCommandResult> CommandResults { get; } = new();

        public Exception? StartException { get; set; }

        public VoiceRecorderProcessExitResult? ImmediateExitResult { get; set; }

        public Func<(string FileName, IReadOnlyList<string> Arguments), IVoiceRecorderProcess>? NextProcessFactory { get; set; }

        public void EnqueueCommandResult(VoiceRecorderCommandResult result) => CommandResults.Enqueue(result);

        public VoiceRecorderCommandResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            CommandCalls.Add((fileName, arguments));
            return CommandResults.Count > 0
                ? CommandResults.Dequeue()
                : new VoiceRecorderCommandResult(0, string.Empty, string.Empty);
        }

        public VoiceRecorderProcessStartResult Start(string fileName, IReadOnlyList<string> arguments)
        {
            Calls.Add((fileName, arguments));
            if (StartException != null)
                return VoiceRecorderProcessStartResult.Error(StartException);

            if (ImmediateExitResult != null)
                return VoiceRecorderProcessStartResult.Exited(ImmediateExitResult);

            return VoiceRecorderProcessStartResult.Success(
                NextProcessFactory?.Invoke((fileName, arguments))
                ?? new StubVoiceRecorderProcess(() => Task.FromResult(new VoiceRecorderProcessExitResult(0, string.Empty))));
        }
    }

    private sealed class StubVoiceRecorderProcess : IVoiceRecorderProcess
    {
        private readonly Func<Task<VoiceRecorderProcessExitResult>> _stop;

        public StubVoiceRecorderProcess(Func<Task<VoiceRecorderProcessExitResult>> stop)
        {
            _stop = stop;
        }

        public Task<VoiceRecorderProcessExitResult> StopAsync(TimeSpan timeout) => _stop();
    }
}
