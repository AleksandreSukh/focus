#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Systems.Sanity.Focus.Application;

internal interface IClipboardCaptureService
{
    ClipboardCaptureResult Capture();
}

internal interface IClipboardTextWriter
{
    ClipboardTextWriteResult CopyText(string text);
}

internal enum ClipboardCaptureKind
{
    Text,
    Image
}

internal sealed record ClipboardCaptureResult(
    ClipboardCaptureKind? Kind,
    string? Text,
    byte[]? ImageBytes,
    string? ErrorMessage)
{
    public bool IsSuccess => Kind.HasValue && string.IsNullOrWhiteSpace(ErrorMessage);

    public static ClipboardCaptureResult TextContent(string text) =>
        new(ClipboardCaptureKind.Text, text, null, null);

    public static ClipboardCaptureResult ImageContent(byte[] imageBytes) =>
        new(ClipboardCaptureKind.Image, null, imageBytes, null);

    public static ClipboardCaptureResult Error(string errorMessage) =>
        new(null, null, null, errorMessage);
}

internal sealed record ClipboardTextWriteResult(string? ErrorMessage)
{
    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);

    public static ClipboardTextWriteResult Success() => new(ErrorMessage: null);

    public static ClipboardTextWriteResult Error(string errorMessage) => new(errorMessage);
}

internal static class ClipboardCaptureServiceFactory
{
    public static IClipboardCaptureService CreateDefault(IClipboardCommandRunner? commandRunner = null)
    {
        commandRunner ??= new ProcessClipboardCommandRunner();

        if (OperatingSystem.IsWindows())
            return new WindowsClipboardCaptureService(commandRunner);

        if (OperatingSystem.IsMacOS())
            return new MacClipboardCaptureService(commandRunner);

        if (OperatingSystem.IsLinux())
            return new LinuxClipboardCaptureService(commandRunner);

        return new UnsupportedClipboardCaptureService();
    }
}

internal static class ClipboardTextWriterFactory
{
    public static IClipboardTextWriter CreateDefault(IClipboardCommandRunner? commandRunner = null)
    {
        commandRunner ??= new ProcessClipboardCommandRunner();

        if (OperatingSystem.IsWindows())
            return new WindowsClipboardTextWriter(commandRunner);

        if (OperatingSystem.IsMacOS())
            return new MacClipboardTextWriter(commandRunner);

        if (OperatingSystem.IsLinux())
            return new LinuxClipboardTextWriter(commandRunner);

        return new UnsupportedClipboardTextWriter();
    }
}

internal interface IClipboardCommandRunner
{
    ClipboardCommandResult Run(string fileName, IReadOnlyList<string> arguments);

    ClipboardCommandResult RunToFile(string fileName, IReadOnlyList<string> arguments, string outputFilePath);

    ClipboardCommandResult RunWithStandardInput(
        string fileName,
        IReadOnlyList<string> arguments,
        string standardInput);
}

internal sealed record ClipboardCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    Exception? StartException = null)
{
    public bool Succeeded => StartException == null && ExitCode == 0;
}

internal sealed class ProcessClipboardCommandRunner : IClipboardCommandRunner
{
    public ClipboardCommandResult Run(string fileName, IReadOnlyList<string> arguments) =>
        RunInternal(fileName, arguments, captureOutput: true);

    public ClipboardCommandResult RunToFile(string fileName, IReadOnlyList<string> arguments, string outputFilePath) =>
        RunInternal(fileName, arguments, captureOutput: false);

    public ClipboardCommandResult RunWithStandardInput(
        string fileName,
        IReadOnlyList<string> arguments,
        string standardInput) =>
        RunInternal(fileName, arguments, captureOutput: false, standardInput: standardInput);

    private static ClipboardCommandResult RunInternal(
        string fileName,
        IReadOnlyList<string> arguments,
        bool captureOutput,
        string? standardInput = null)
    {
        try
        {
            using var process = new Process();
            var redirectStandardInput = standardInput != null;
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardInput = redirectStandardInput,
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            if (redirectStandardInput)
            {
                process.StartInfo.StandardInputEncoding = Encoding.UTF8;
            }

            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            if (redirectStandardInput)
            {
                process.StandardInput.Write(standardInput);
                process.StandardInput.Close();
            }

            var standardOutput = captureOutput
                ? process.StandardOutput.ReadToEnd()
                : string.Empty;
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new ClipboardCommandResult(process.ExitCode, standardOutput, standardError);
        }
        catch (Exception ex)
        {
            return new ClipboardCommandResult(-1, string.Empty, string.Empty, ex);
        }
    }
}

internal sealed class UnsupportedClipboardCaptureService : IClipboardCaptureService
{
    public ClipboardCaptureResult Capture() =>
        ClipboardCaptureResult.Error("Clipboard capture is not supported on this operating system.");
}

internal sealed class UnsupportedClipboardTextWriter : IClipboardTextWriter
{
    public ClipboardTextWriteResult CopyText(string text) =>
        ClipboardTextWriteResult.Error("Clipboard text export is not supported on this operating system.");
}

internal sealed class WindowsClipboardTextWriter : IClipboardTextWriter
{
    private readonly IClipboardCommandRunner _commandRunner;

    public WindowsClipboardTextWriter(IClipboardCommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public ClipboardTextWriteResult CopyText(string text)
    {
        var commandResult = _commandRunner.RunWithStandardInput(
            "powershell",
            new[]
            {
                "-NoProfile",
                "-STA",
                "-Command",
                "[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false); $text = [Console]::In.ReadToEnd(); Set-Clipboard -Value $text"
            },
            text ?? string.Empty);

        if (commandResult.StartException != null)
            return ClipboardTextWriteResult.Error("Clipboard text export on Windows requires PowerShell.");

        return commandResult.Succeeded
            ? ClipboardTextWriteResult.Success()
            : ClipboardTextWriteResult.Error(ClipboardTextWriteHelpers.BuildCommandError(commandResult));
    }
}

internal sealed class MacClipboardTextWriter : IClipboardTextWriter
{
    private readonly IClipboardCommandRunner _commandRunner;

    public MacClipboardTextWriter(IClipboardCommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public ClipboardTextWriteResult CopyText(string text)
    {
        var commandResult = _commandRunner.RunWithStandardInput(
            "pbcopy",
            Array.Empty<string>(),
            text ?? string.Empty);

        if (commandResult.StartException != null)
            return ClipboardTextWriteResult.Error("Clipboard text export on macOS requires \"pbcopy\".");

        return commandResult.Succeeded
            ? ClipboardTextWriteResult.Success()
            : ClipboardTextWriteResult.Error(ClipboardTextWriteHelpers.BuildCommandError(commandResult));
    }
}

internal sealed class LinuxClipboardTextWriter : IClipboardTextWriter
{
    private readonly IClipboardCommandRunner _commandRunner;

    public LinuxClipboardTextWriter(IClipboardCommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public ClipboardTextWriteResult CopyText(string text)
    {
        var normalizedText = text ?? string.Empty;
        var wlResult = _commandRunner.RunWithStandardInput(
            "wl-copy",
            Array.Empty<string>(),
            normalizedText);
        if (wlResult.Succeeded)
            return ClipboardTextWriteResult.Success();

        var xclipResult = _commandRunner.RunWithStandardInput(
            "xclip",
            new[] { "-selection", "clipboard" },
            normalizedText);
        if (xclipResult.Succeeded)
            return ClipboardTextWriteResult.Success();

        if (wlResult.StartException != null && xclipResult.StartException != null)
            return ClipboardTextWriteResult.Error("Clipboard text export on Linux requires \"wl-copy\" or \"xclip\".");

        if (xclipResult.StartException == null && !xclipResult.Succeeded)
            return ClipboardTextWriteResult.Error(ClipboardTextWriteHelpers.BuildCommandError(xclipResult));

        if (wlResult.StartException == null && !wlResult.Succeeded)
            return ClipboardTextWriteResult.Error(ClipboardTextWriteHelpers.BuildCommandError(wlResult));

        return ClipboardTextWriteResult.Error("Couldn't copy text to clipboard.");
    }
}

internal sealed class WindowsClipboardCaptureService : IClipboardCaptureService
{
    private const int NoImageExitCode = 3;
    private const int NoTextExitCode = 4;

    private readonly IClipboardCommandRunner _commandRunner;

    public WindowsClipboardCaptureService(IClipboardCommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public ClipboardCaptureResult Capture()
    {
        var imageResult = TryCaptureImage();
        if (imageResult.IsSuccess)
            return imageResult;

        var textResult = TryCaptureText();
        if (textResult.IsSuccess)
            return textResult;

        if (imageResult.ErrorMessage != null)
            return imageResult;

        return textResult.ErrorMessage != null
            ? textResult
            : ClipboardCaptureResult.Error("Clipboard is empty or doesn't contain supported text/image data.");
    }

    private ClipboardCaptureResult TryCaptureImage()
    {
        var tempFilePath = ClipboardCaptureHelpers.BuildTempPngPath();
        try
        {
            var commandResult = _commandRunner.RunToFile(
                "powershell",
                new[]
                {
                    "-NoProfile",
                    "-STA",
                    "-Command",
                    BuildWindowsImageScript(tempFilePath)
                },
                tempFilePath);

            if (commandResult.StartException != null)
                return ClipboardCaptureResult.Error("Clipboard image capture on Windows requires PowerShell.");

            if (commandResult.ExitCode == NoImageExitCode)
                return ClipboardCaptureResult.Error(string.Empty);

            if (!commandResult.Succeeded)
                return ClipboardCaptureResult.Error(ClipboardCaptureHelpers.BuildCommandError("clipboard image", commandResult));

            if (!File.Exists(tempFilePath))
                return ClipboardCaptureResult.Error("Clipboard image capture did not produce an attachment file.");

            return ClipboardCaptureResult.ImageContent(File.ReadAllBytes(tempFilePath));
        }
        finally
        {
            ClipboardCaptureHelpers.DeleteFileIfExists(tempFilePath);
        }
    }

    private ClipboardCaptureResult TryCaptureText()
    {
        var commandResult = _commandRunner.Run(
            "powershell",
            new[]
            {
                "-NoProfile",
                "-STA",
                "-Command",
                "try { $text = Get-Clipboard -Raw -Format Text } catch { $text = $null }; if ([string]::IsNullOrEmpty($text)) { exit 4 }; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; [Console]::Write($text)"
            });

        if (commandResult.StartException != null)
            return ClipboardCaptureResult.Error("Clipboard text capture on Windows requires PowerShell.");

        if (commandResult.ExitCode == NoTextExitCode)
            return ClipboardCaptureResult.Error("Clipboard is empty or doesn't contain supported text/image data.");

        if (!commandResult.Succeeded)
            return ClipboardCaptureResult.Error(ClipboardCaptureHelpers.BuildCommandError("clipboard text", commandResult));

        return string.IsNullOrWhiteSpace(commandResult.StandardOutput)
            ? ClipboardCaptureResult.Error("Clipboard is empty or doesn't contain supported text/image data.")
            : ClipboardCaptureResult.TextContent(commandResult.StandardOutput);
    }

    private static string BuildWindowsImageScript(string tempFilePath)
    {
        var escapedPath = tempFilePath.Replace("'", "''", StringComparison.Ordinal);
        return string.Join(" ", new[]
        {
            "Add-Type -AssemblyName System.Windows.Forms;",
            "Add-Type -AssemblyName System.Drawing;",
            "$image = [System.Windows.Forms.Clipboard]::GetImage();",
            "if ($null -eq $image) { exit 3 };",
            $"$image.Save('{escapedPath}', [System.Drawing.Imaging.ImageFormat]::Png);",
            "$image.Dispose();"
        });
    }
}

internal sealed class MacClipboardCaptureService : IClipboardCaptureService
{
    private readonly IClipboardCommandRunner _commandRunner;

    public MacClipboardCaptureService(IClipboardCommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public ClipboardCaptureResult Capture()
    {
        var imageResult = TryCaptureImage();
        if (imageResult.IsSuccess)
            return imageResult;

        var textResult = TryCaptureText();
        if (textResult.IsSuccess)
            return textResult;

        if (imageResult.ErrorMessage == "missing:pngpaste")
            return ClipboardCaptureResult.Error("Clipboard image capture on macOS requires \"pngpaste\".");

        return textResult.ErrorMessage != null
            ? textResult
            : ClipboardCaptureResult.Error("Clipboard is empty or doesn't contain supported text/image data.");
    }

    private ClipboardCaptureResult TryCaptureImage()
    {
        var tempFilePath = ClipboardCaptureHelpers.BuildTempPngPath();
        try
        {
            var commandResult = _commandRunner.RunToFile(
                "pngpaste",
                new[] { tempFilePath },
                tempFilePath);

            if (commandResult.StartException != null)
                return ClipboardCaptureResult.Error("missing:pngpaste");

            if (!commandResult.Succeeded || !File.Exists(tempFilePath))
                return ClipboardCaptureResult.Error(string.Empty);

            return ClipboardCaptureResult.ImageContent(File.ReadAllBytes(tempFilePath));
        }
        finally
        {
            ClipboardCaptureHelpers.DeleteFileIfExists(tempFilePath);
        }
    }

    private ClipboardCaptureResult TryCaptureText()
    {
        var commandResult = _commandRunner.Run("pbpaste", Array.Empty<string>());
        if (commandResult.StartException != null)
            return ClipboardCaptureResult.Error("Clipboard text capture on macOS requires \"pbpaste\".");

        if (!commandResult.Succeeded)
            return ClipboardCaptureResult.Error(ClipboardCaptureHelpers.BuildCommandError("clipboard text", commandResult));

        return string.IsNullOrWhiteSpace(commandResult.StandardOutput)
            ? ClipboardCaptureResult.Error("Clipboard is empty or doesn't contain supported text/image data.")
            : ClipboardCaptureResult.TextContent(commandResult.StandardOutput);
    }
}

internal sealed class LinuxClipboardCaptureService : IClipboardCaptureService
{
    private readonly IClipboardCommandRunner _commandRunner;

    public LinuxClipboardCaptureService(IClipboardCommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public ClipboardCaptureResult Capture()
    {
        var imageResult = TryCaptureImage();
        if (imageResult.IsSuccess)
            return imageResult;

        var textResult = TryCaptureText();
        if (textResult.IsSuccess)
            return textResult;

        if (imageResult.ErrorMessage == "missing:linux-tools" || textResult.ErrorMessage == "missing:linux-tools")
        {
            return ClipboardCaptureResult.Error(
                "Clipboard capture on Linux requires \"wl-paste\" or \"xclip\".");
        }

        return textResult.ErrorMessage != null
            ? textResult
            : ClipboardCaptureResult.Error("Clipboard is empty or doesn't contain supported text/image data.");
    }

    private ClipboardCaptureResult TryCaptureImage()
    {
        var tempFilePath = ClipboardCaptureHelpers.BuildTempPngPath();
        try
        {
            var wlTypeResult = _commandRunner.Run("wl-paste", new[] { "--list-types" });
            if (wlTypeResult.StartException == null &&
                wlTypeResult.Succeeded &&
                ContainsImageMime(wlTypeResult.StandardOutput))
            {
                var captureResult = _commandRunner.RunToFile(
                    "/bin/sh",
                    new[] { "-lc", $"wl-paste --no-newline --type image/png > {QuoteForShell(tempFilePath)}" },
                    tempFilePath);
                if (captureResult.Succeeded && File.Exists(tempFilePath))
                    return ClipboardCaptureResult.ImageContent(File.ReadAllBytes(tempFilePath));
            }

            var xclipTypeResult = _commandRunner.Run(
                "xclip",
                new[] { "-selection", "clipboard", "-t", "TARGETS", "-o" });
            if (xclipTypeResult.StartException == null &&
                xclipTypeResult.Succeeded &&
                ContainsImageMime(xclipTypeResult.StandardOutput))
            {
                var captureResult = _commandRunner.RunToFile(
                    "/bin/sh",
                    new[] { "-lc", $"xclip -selection clipboard -t image/png -o > {QuoteForShell(tempFilePath)}" },
                    tempFilePath);
                if (captureResult.Succeeded && File.Exists(tempFilePath))
                    return ClipboardCaptureResult.ImageContent(File.ReadAllBytes(tempFilePath));
            }

            if (wlTypeResult.StartException != null && xclipTypeResult.StartException != null)
                return ClipboardCaptureResult.Error("missing:linux-tools");

            return ClipboardCaptureResult.Error(string.Empty);
        }
        finally
        {
            ClipboardCaptureHelpers.DeleteFileIfExists(tempFilePath);
        }
    }

    private ClipboardCaptureResult TryCaptureText()
    {
        var wlResult = _commandRunner.Run("wl-paste", new[] { "--no-newline", "--type", "text" });
        if (wlResult.StartException == null && wlResult.Succeeded && !string.IsNullOrWhiteSpace(wlResult.StandardOutput))
            return ClipboardCaptureResult.TextContent(wlResult.StandardOutput);

        var xclipResult = _commandRunner.Run("xclip", new[] { "-selection", "clipboard", "-o" });
        if (xclipResult.StartException == null &&
            xclipResult.Succeeded &&
            !string.IsNullOrWhiteSpace(xclipResult.StandardOutput))
        {
            return ClipboardCaptureResult.TextContent(xclipResult.StandardOutput);
        }

        if (wlResult.StartException != null && xclipResult.StartException != null)
            return ClipboardCaptureResult.Error("missing:linux-tools");

        if (!wlResult.Succeeded && wlResult.StartException == null && !string.IsNullOrWhiteSpace(wlResult.StandardError))
            return ClipboardCaptureResult.Error(ClipboardCaptureHelpers.BuildCommandError("clipboard text", wlResult));

        if (!xclipResult.Succeeded && xclipResult.StartException == null && !string.IsNullOrWhiteSpace(xclipResult.StandardError))
            return ClipboardCaptureResult.Error(ClipboardCaptureHelpers.BuildCommandError("clipboard text", xclipResult));

        return ClipboardCaptureResult.Error("Clipboard is empty or doesn't contain supported text/image data.");
    }

    private static bool ContainsImageMime(string output) =>
        output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => line.StartsWith("image/", StringComparison.OrdinalIgnoreCase));

    private static string QuoteForShell(string value) =>
        $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
}

internal static class ClipboardCaptureHelpers
{
    public static string BuildTempPngPath() =>
        Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");

    public static void DeleteFileIfExists(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    public static string BuildCommandError(string actionLabel, ClipboardCommandResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardError))
            return $"Couldn't capture {actionLabel}: {result.StandardError.Trim()}";

        return $"Couldn't capture {actionLabel}.";
    }
}

internal static class ClipboardTextWriteHelpers
{
    public static string BuildCommandError(ClipboardCommandResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardError))
            return $"Couldn't copy text to clipboard: {result.StandardError.Trim()}";

        return "Couldn't copy text to clipboard.";
    }
}
