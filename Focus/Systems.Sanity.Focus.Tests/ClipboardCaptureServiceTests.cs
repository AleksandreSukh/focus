using System.IO;
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
}
