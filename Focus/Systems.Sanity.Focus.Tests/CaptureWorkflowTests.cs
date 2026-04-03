using System.IO;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Tests;

public class CaptureWorkflowTests
{
    [Fact]
    public void Execute_CaptureText_AddsAttachmentToCurrentNode()
    {
        const string clipboardText = "Unicode note \u041f\u0440\u0438\u0432\u0435\u0442 \u043c\u0438\u0440\nwith multiple lines and more details for preview handling";
        using var workspace = new TestWorkspace(
            clipboardCaptureService: new FakeClipboardCaptureService(
                ClipboardCaptureResult.TextContent(clipboardText)));
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("capture"));
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var attachment = reopened.RootNode.Metadata!.Attachments.Single();
        var attachmentPath = workspace.AppContext.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            filePath,
            attachment.RelativePath);

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("Capture clipboard in workflow-map", result.SyncCommitMessage);
        Assert.Equal("Root", reopened.RootNode.Name);
        Assert.Empty(reopened.GetChildren());
        Assert.Equal("text/plain; charset=utf-8", attachment.MediaType);
        Assert.StartsWith("Clipboard text ", attachment.DisplayName);
        Assert.Equal(clipboardText, File.ReadAllText(attachmentPath, Encoding.UTF8));
    }

    [Fact]
    public void Execute_CaptureImage_AddsAttachmentToCurrentNode()
    {
        using var workspace = new TestWorkspace(
            clipboardCaptureService: new FakeClipboardCaptureService(
                ClipboardCaptureResult.ImageContent(Encoding.UTF8.GetBytes("fake-png"))));
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("capture"));
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var attachment = reopened.RootNode.Metadata!.Attachments.Single();

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("image/png", attachment.MediaType);
        Assert.EndsWith(".png", attachment.DisplayName);
        Assert.Empty(reopened.GetChildren());
    }

    [Fact]
    public void Execute_CaptureClipboardError_ReturnsErrorWithoutPersistence()
    {
        using var workspace = new TestWorkspace(
            clipboardCaptureService: new FakeClipboardCaptureService(
                ClipboardCaptureResult.Error("Clipboard is empty")));
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("capture"));

        Assert.False(result.IsSuccess);
        Assert.False(result.ShouldPersist);
        Assert.Equal("Clipboard is empty", result.ErrorString);
    }

    [Fact]
    public void Execute_Meta_ShowsMetadataWithoutPersistence()
    {
        using var workspace = new TestWorkspace();
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession());
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("meta"));

        Assert.True(result.IsSuccess);
        Assert.False(result.ShouldPersist);
    }

    [Fact]
    public void Execute_Attachments_OpensCurrentAttachmentByShortcut()
    {
        var fileOpener = new RecordingFileOpener();
        using var workspace = new TestWorkspace(fileOpener: fileOpener);
        var map = new MindMap("Root");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var filePath = workspace.SaveMap("workflow-map", map);
        var attachmentPath = workspace.AppContext.MapsStorage.AttachmentStore.ResolveAttachmentPath(filePath, "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(attachmentPath)!);
        File.WriteAllText(attachmentPath, "attachment");

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput("attachments 1"));

        Assert.True(result.IsSuccess);
        Assert.Equal(attachmentPath, fileOpener.OpenedFilePath);
    }

    [Fact]
    public void Execute_AttachmentShortcut_OpensCurrentAttachmentWhenNoChildrenMatch()
    {
        var fileOpener = new RecordingFileOpener();
        using var workspace = new TestWorkspace(fileOpener: fileOpener);
        var map = new MindMap("Root");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var filePath = workspace.SaveMap("workflow-map", map);
        var attachmentPath = workspace.AppContext.MapsStorage.AttachmentStore.ResolveAttachmentPath(filePath, "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(attachmentPath)!);
        File.WriteAllText(attachmentPath, "attachment");

        var workflow = new EditWorkflow(filePath, workspace.AppContext);
        var result = workflow.Execute(new ConsoleInput(AccessibleKeyNumbering.GetStringFor(1)));

        Assert.True(result.IsSuccess);
        Assert.Equal(attachmentPath, fileOpener.OpenedFilePath);
    }

    [Fact]
    public void Execute_Attachments_ReturnsErrorWhenAttachmentFileIsMissing()
    {
        using var workspace = new TestWorkspace(fileOpener: new RecordingFileOpener());
        var map = new MindMap("Root");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "missing.png",
            MediaType = "image/png",
            DisplayName = "Missing.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("attachments 1"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Attachment \"Missing.png\" is missing", result.ErrorString);
    }

    [Fact]
    public void Execute_Attachments_WhenOpenerThrows_ReturnsGenericErrorAndLogsException()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        var fileOpener = new RecordingFileOpener
        {
            ExceptionToThrow = new InvalidOperationException("shell launch failed")
        };
        using var workspace = new TestWorkspace(fileOpener: fileOpener);
        var map = new MindMap("Root");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var filePath = workspace.SaveMap("workflow-map", map);
        var attachmentPath = workspace.AppContext.MapsStorage.AttachmentStore.ResolveAttachmentPath(filePath, "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(attachmentPath)!);
        File.WriteAllText(attachmentPath, "attachment");

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("1"));
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("attachments"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExceptionDiagnostics.BuildUserMessage("opening attachment"), result.ErrorString);
        Assert.Contains("Action: opening attachment", diagnosticsScope.ReadLog());
        Assert.Contains("shell launch failed", diagnosticsScope.ReadLog());
    }

    [Fact]
    public void Execute_Capture_WhenClipboardServiceThrows_ReturnsGenericErrorAndLogsException()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        using var workspace = new TestWorkspace(
            clipboardCaptureService: new ThrowingClipboardCaptureService(
                new InvalidOperationException("clipboard service unavailable")));
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("capture"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExceptionDiagnostics.BuildUserMessage("capturing clipboard"), result.ErrorString);
        Assert.Contains("Action: capturing clipboard", diagnosticsScope.ReadLog());
        Assert.Contains("clipboard service unavailable", diagnosticsScope.ReadLog());
    }

    [Fact]
    public void GetSuggestions_IncludeCaptureMetadataAndAttachmentCommands()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var suggestions = workflow.GetSuggestions().ToArray();

        Assert.Contains("capture", suggestions);
        Assert.Contains("meta", suggestions);
        Assert.Contains("attachments", suggestions);
        Assert.Contains("attachments 1", suggestions);
        Assert.Contains($"attachments {AccessibleKeyNumbering.GetStringFor(1)}", suggestions);
        Assert.Contains("1", suggestions);
        Assert.Contains(AccessibleKeyNumbering.GetStringFor(1), suggestions);
    }
}
