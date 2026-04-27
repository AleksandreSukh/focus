using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Application;

namespace Systems.Sanity.Focus.Tests;

public class ClipboardCaptureServiceTests
{
    [Fact]
    public void WindowsCapture_PrefersClipboardImageWhenAvailable()
    {
        var commandRunner = new RecordingClipboardCommandRunner();
        commandRunner.EnqueueResponse((_, _, outputFilePath) =>
        {
            File.WriteAllBytes(outputFilePath!, [1, 2, 3, 4]);
            return new ClipboardCommandResult(0, string.Empty, string.Empty);
        });
        var service = new WindowsClipboardCaptureService(commandRunner);

        var result = service.Capture();

        Assert.True(result.IsSuccess);
        Assert.Equal(ClipboardCaptureKind.Image, result.Kind);
        Assert.Equal([1, 2, 3, 4], result.ImageBytes);
    }

    [Fact]
    public void WindowsCapture_FallsBackToClipboardTextWhenImageIsUnavailable()
    {
        var commandRunner = new RecordingClipboardCommandRunner();
        commandRunner.EnqueueResponse((_, _, _) => new ClipboardCommandResult(3, string.Empty, string.Empty));
        commandRunner.EnqueueResponse((_, _, _) => new ClipboardCommandResult(0, "Line 1\r\nLine 2", string.Empty));
        var service = new WindowsClipboardCaptureService(commandRunner);

        var result = service.Capture();

        Assert.True(result.IsSuccess);
        Assert.Equal(ClipboardCaptureKind.Text, result.Kind);
        Assert.Equal("Line 1\r\nLine 2", result.Text);
    }

    [Fact]
    public void LinuxCapture_ReturnsHelpfulErrorWhenClipboardToolsAreMissing()
    {
        var commandRunner = new RecordingClipboardCommandRunner();
        for (var index = 0; index < 4; index++)
        {
            commandRunner.EnqueueResponse((_, _, _) =>
                new ClipboardCommandResult(-1, string.Empty, string.Empty, new FileNotFoundException("missing")));
        }

        var service = new LinuxClipboardCaptureService(commandRunner);

        var result = service.Capture();

        Assert.False(result.IsSuccess);
        Assert.Equal("Clipboard capture on Linux requires \"wl-paste\" or \"xclip\".", result.ErrorMessage);
    }

    [Fact]
    public void WindowsCopyText_UsesPowerShellWithStandardInput()
    {
        var commandRunner = new RecordingClipboardCommandRunner();
        commandRunner.EnqueueResponse((_, _, _, _) => new ClipboardCommandResult(0, string.Empty, string.Empty));
        var writer = new WindowsClipboardTextWriter(commandRunner);

        var result = writer.CopyText("Line 1\nLine 2");
        var call = commandRunner.Calls.Single();

        Assert.True(result.IsSuccess);
        Assert.Equal("powershell", call.FileName);
        Assert.Contains(call.Arguments, argument => argument.Contains("Set-Clipboard", StringComparison.Ordinal));
        Assert.Equal("Line 1\nLine 2", call.StandardInput);
    }

    [Fact]
    public void MacCopyText_UsesPbcopyWithStandardInput()
    {
        var commandRunner = new RecordingClipboardCommandRunner();
        commandRunner.EnqueueResponse((_, _, _, _) => new ClipboardCommandResult(0, string.Empty, string.Empty));
        var writer = new MacClipboardTextWriter(commandRunner);

        var result = writer.CopyText("Hello chat");
        var call = commandRunner.Calls.Single();

        Assert.True(result.IsSuccess);
        Assert.Equal("pbcopy", call.FileName);
        Assert.Equal("Hello chat", call.StandardInput);
    }

    [Fact]
    public void LinuxCopyText_PrefersWlCopy()
    {
        var commandRunner = new RecordingClipboardCommandRunner();
        commandRunner.EnqueueResponse((_, _, _, _) => new ClipboardCommandResult(0, string.Empty, string.Empty));
        var writer = new LinuxClipboardTextWriter(commandRunner);

        var result = writer.CopyText("Hello chat");

        Assert.True(result.IsSuccess);
        Assert.Equal(["wl-copy"], commandRunner.Calls.Select(call => call.FileName).ToArray());
        Assert.Equal("Hello chat", commandRunner.Calls.Single().StandardInput);
    }

    [Fact]
    public void LinuxCopyText_FallsBackToXclip()
    {
        var commandRunner = new RecordingClipboardCommandRunner();
        commandRunner.EnqueueResponse((_, _, _, _) =>
            new ClipboardCommandResult(-1, string.Empty, string.Empty, new FileNotFoundException("missing")));
        commandRunner.EnqueueResponse((_, _, _, _) => new ClipboardCommandResult(0, string.Empty, string.Empty));
        var writer = new LinuxClipboardTextWriter(commandRunner);

        var result = writer.CopyText("Hello chat");

        Assert.True(result.IsSuccess);
        Assert.Equal(["wl-copy", "xclip"], commandRunner.Calls.Select(call => call.FileName).ToArray());
        Assert.Equal("Hello chat", commandRunner.Calls.Last().StandardInput);
    }

    [Fact]
    public void LinuxCopyText_ReturnsHelpfulErrorWhenClipboardToolsAreMissing()
    {
        var commandRunner = new RecordingClipboardCommandRunner();
        commandRunner.EnqueueResponse((_, _, _, _) =>
            new ClipboardCommandResult(-1, string.Empty, string.Empty, new FileNotFoundException("missing")));
        commandRunner.EnqueueResponse((_, _, _, _) =>
            new ClipboardCommandResult(-1, string.Empty, string.Empty, new FileNotFoundException("missing")));
        var writer = new LinuxClipboardTextWriter(commandRunner);

        var result = writer.CopyText("Hello chat");

        Assert.False(result.IsSuccess);
        Assert.Equal("Clipboard text export on Linux requires \"wl-copy\" or \"xclip\".", result.ErrorMessage);
    }
}
