#nullable enable

using System;
using System.IO;
using System.Text;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Application;

internal static class TestHostAppOverrides
{
    private const string ClipboardModeEnvironmentVariable = "FOCUS_TEST_CLIPBOARD_MODE";
    private const string ClipboardTextBase64EnvironmentVariable = "FOCUS_TEST_CLIPBOARD_TEXT_BASE64";
    private const string ClipboardImageBase64EnvironmentVariable = "FOCUS_TEST_CLIPBOARD_IMAGE_BASE64";
    private const string ClipboardErrorEnvironmentVariable = "FOCUS_TEST_CLIPBOARD_ERROR";
    private const string OpenedFilesLogPathEnvironmentVariable = "FOCUS_TEST_OPENED_FILES_LOG";

    public static FocusAppContext CreateContext(MapsStorage mapsStorage, UserConfig? userConfig = null)
    {
        var clipboardCaptureService = CreateClipboardCaptureService();
        var fileOpener = CreateFileOpener();
        var voiceRecorder = userConfig?.VoiceRecorder == null
            ? null
            : VoiceRecorderFactory.CreateDefault(userConfig.VoiceRecorder);

        if (clipboardCaptureService == null && fileOpener == null && voiceRecorder == null)
            return new FocusAppContext(mapsStorage);

        return new FocusAppContext(
            mapsStorage,
            navigator: null,
            clipboardCaptureService: clipboardCaptureService,
            fileOpener: fileOpener,
            voiceRecorder: voiceRecorder);
    }

    private static IClipboardCaptureService? CreateClipboardCaptureService()
    {
        var clipboardMode = Environment.GetEnvironmentVariable(ClipboardModeEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(clipboardMode))
            return null;

        if (string.Equals(clipboardMode.Trim(), "throw", StringComparison.OrdinalIgnoreCase))
        {
            return new ThrowingClipboardCaptureService(
                new InvalidOperationException(
                    Environment.GetEnvironmentVariable(ClipboardErrorEnvironmentVariable) ?? "Clipboard capture failed"));
        }

        var result = clipboardMode.Trim().ToLowerInvariant() switch
        {
            "text" => ClipboardCaptureResult.TextContent(
                DecodeBase64Utf8Text(ClipboardTextBase64EnvironmentVariable)),
            "image" => ClipboardCaptureResult.ImageContent(
                DecodeBase64Bytes(ClipboardImageBase64EnvironmentVariable)),
            "error" => ClipboardCaptureResult.Error(
                Environment.GetEnvironmentVariable(ClipboardErrorEnvironmentVariable) ?? "Clipboard capture failed"),
            _ => throw new InvalidOperationException(
                $"Unsupported test clipboard mode \"{clipboardMode}\".")
        };

        return new StaticClipboardCaptureService(result);
    }

    private static IFileOpener? CreateFileOpener()
    {
        var openedFilesLogPath = Environment.GetEnvironmentVariable(OpenedFilesLogPathEnvironmentVariable);
        return string.IsNullOrWhiteSpace(openedFilesLogPath)
            ? null
            : new RecordingFileOpener(openedFilesLogPath);
    }

    private static string DecodeBase64Utf8Text(string environmentVariableName) =>
        Encoding.UTF8.GetString(DecodeBase64Bytes(environmentVariableName));

    private static byte[] DecodeBase64Bytes(string environmentVariableName)
    {
        var encodedValue = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(encodedValue))
        {
            throw new InvalidOperationException(
                $"Missing required test environment variable \"{environmentVariableName}\".");
        }

        return Convert.FromBase64String(encodedValue);
    }

    private sealed class StaticClipboardCaptureService : IClipboardCaptureService
    {
        private readonly ClipboardCaptureResult _result;

        public StaticClipboardCaptureService(ClipboardCaptureResult result)
        {
            _result = result;
        }

        public ClipboardCaptureResult Capture() => _result;
    }

    private sealed class RecordingFileOpener : IFileOpener
    {
        private readonly string _openedFilesLogPath;

        public RecordingFileOpener(string openedFilesLogPath)
        {
            _openedFilesLogPath = openedFilesLogPath;
        }

        public bool TryOpen(string filePath, out string? errorMessage)
        {
            var logDirectory = Path.GetDirectoryName(_openedFilesLogPath);
            if (!string.IsNullOrWhiteSpace(logDirectory))
                Directory.CreateDirectory(logDirectory);

            File.AppendAllText(
                _openedFilesLogPath,
                $"{filePath}{Environment.NewLine}",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            errorMessage = null;
            return true;
        }
    }

    private sealed class ThrowingClipboardCaptureService : IClipboardCaptureService
    {
        private readonly Exception _exception;

        public ThrowingClipboardCaptureService(Exception exception)
        {
            _exception = exception;
        }

        public ClipboardCaptureResult Capture()
        {
            throw _exception;
        }
    }
}
