using System.IO;
using System.Threading;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Pages;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;

namespace Systems.Sanity.Focus.Tests;

public class AttachmentLifecycleTests
{
    [Fact]
    public void CreateMapPage_Show_PreservesNodeScopedAttachmentsForDetachedMap()
    {
        var navigator = new RecordingPageNavigator();
        using var workspace = new TestWorkspace(navigator);
        var sourceMap = new MindMap("Source");
        var detachedNode = sourceMap.AddAtCurrentNode("Detached");
        detachedNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var sourceFilePath = workspace.SaveMap("source", sourceMap);
        var sourceAttachmentPath = workspace.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            sourceFilePath,
            GetRequiredNodeIdentifier(detachedNode),
            "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceAttachmentPath)!);
        File.WriteAllText(sourceAttachmentPath, "attachment");

        var detachedMap = new MindMap(detachedNode);

        new CreateMapPage(workspace.AppContext, "detached", detachedMap, sourceFilePath).Show();

        Assert.NotNull(navigator.OpenedEditMapFilePath);
        var targetAttachmentPath = workspace.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            navigator.OpenedEditMapFilePath!,
            GetRequiredNodeIdentifier(detachedMap.RootNode),
            detachedMap.RootNode.Metadata!.Attachments[0].RelativePath);
        Assert.Equal(sourceAttachmentPath, targetAttachmentPath);
        Assert.True(File.Exists(targetAttachmentPath));
    }

    [Fact]
    public void AttachMode_Show_PreservesNodeScopedAttachmentsAndDeletesSourceMap()
    {
        using var workspace = new TestWorkspace();
        var targetMap = new MindMap("Target");
        var targetFilePath = workspace.SaveMap("target", targetMap);
        Thread.Sleep(20);

        var sourceMap = new MindMap("Source");
        sourceMap.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var sourceFilePath = workspace.SaveMap("source", sourceMap);
        var sourceAttachmentPath = workspace.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            sourceFilePath,
            GetRequiredNodeIdentifier(sourceMap.RootNode),
            "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceAttachmentPath)!);
        File.WriteAllText(sourceAttachmentPath, "attachment");

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("1"));
        var attachMode = new AttachMode(targetMap, workspace.AppContext, targetFilePath);

        attachMode.Show();

        Assert.True(attachMode.DidAttachMap);
        Assert.Equal("Source", targetMap.GetChildren()[1]);
        Assert.False(File.Exists(sourceFilePath));
        var attachedChild = targetMap.GetNode("1");
        Assert.NotNull(attachedChild);
        var targetAttachmentPath = workspace.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            targetFilePath,
            GetRequiredNodeIdentifier(attachedChild!),
            attachedChild!.Metadata!.Attachments[0].RelativePath);
        Assert.Equal(sourceAttachmentPath, targetAttachmentPath);
        Assert.True(File.Exists(targetAttachmentPath));
    }

    private static Guid GetRequiredNodeIdentifier(Node node) =>
        node.UniqueIdentifier ?? throw new InvalidOperationException("Node identifier is required for attachment tests.");
}
