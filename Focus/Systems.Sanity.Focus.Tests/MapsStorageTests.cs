using Systems.Sanity.Focus.Domain;
using Newtonsoft.Json.Linq;

namespace Systems.Sanity.Focus.Tests;

public class MapsStorageTests
{
    [Fact]
    public void SaveAndOpenMap_RoundTripsRootAndChildren()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("First child");
        map.AddAtCurrentNode("Second child");

        var filePath = workspace.SaveMap("alpha", map);
        var reopened = workspace.MapsStorage.OpenMap(filePath);

        Assert.Equal("Root", reopened.RootNode.Name);
        Assert.Equal(2, reopened.GetChildren().Count);
        Assert.Equal("First child", reopened.GetChildren()[1]);
        Assert.Equal("Second child", reopened.GetChildren()[2]);
    }

    [Fact]
    public void SaveAndOpenMap_RoundTripsNodeMetadataAndAttachments()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.RootNode.HideDoneTasks = true;
        var child = map.AddAtCurrentNode("Captured note");
        child.HideDoneTasks = true;
        child.Metadata!.Source = NodeMetadataSources.ClipboardText;
        child.Metadata.Device = "device-a";
        child.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var filePath = workspace.SaveMap("alpha", map);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var reopenedChild = reopened.GetNode("1");

        Assert.True(reopened.RootNode.HideDoneTasks);
        Assert.NotNull(reopenedChild);
        Assert.NotNull(reopenedChild!.Metadata);
        Assert.True(reopenedChild.HideDoneTasks);
        Assert.Equal(NodeMetadataSources.ClipboardText, reopenedChild.Metadata!.Source);
        Assert.Equal("device-a", reopenedChild.Metadata.Device);
        Assert.Single(reopenedChild.Metadata.Attachments);
        Assert.Equal("capture.png", reopenedChild.Metadata.Attachments[0].RelativePath);
    }

    [Fact]
    public void OpenMap_BackfillsLegacyMetadataWithoutPersistingUntilSave()
    {
        using var workspace = new TestWorkspace();
        var filePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "legacy.json");
        File.WriteAllText(
            filePath,
            """
            {
              "rootNode": {
                "uniqueIdentifier": "11111111-1111-1111-1111-111111111111",
                "name": "Root",
                "children": [],
                "links": {},
                "number": 1,
                "collapsed": false,
                "taskState": 0
              }
            }
            """);

        var legacyTimestampUtc = new DateTimeOffset(2024, 02, 03, 04, 05, 06, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(filePath, legacyTimestampUtc.UtcDateTime);

        var reopened = workspace.MapsStorage.OpenMap(filePath);

        Assert.NotNull(reopened.RootNode.Metadata);
        Assert.Equal(NodeMetadataSources.LegacyImport, reopened.RootNode.Metadata!.Source);
        Assert.Equal(legacyTimestampUtc, reopened.RootNode.Metadata.CreatedAtUtc);
        Assert.Equal(legacyTimestampUtc, reopened.RootNode.Metadata.UpdatedAtUtc);
        Assert.DoesNotContain("\"metadata\"", File.ReadAllText(filePath), StringComparison.OrdinalIgnoreCase);

        workspace.MapsStorage.SaveMap(filePath, reopened);

        var persistedJson = JObject.Parse(File.ReadAllText(filePath));
        Assert.NotNull(persistedJson["rootNode"]?["metadata"]);
    }

    [Fact]
    public void MoveMap_RenamesAttachmentDirectory()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var sourceFilePath = workspace.SaveMap("alpha", map);
        var sourceAttachmentDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(sourceFilePath);
        Directory.CreateDirectory(sourceAttachmentDirectory);
        File.WriteAllText(Path.Combine(sourceAttachmentDirectory, "capture.png"), "attachment");

        var targetFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "beta.json");

        workspace.MapsStorage.MoveMap(sourceFilePath, targetFilePath);

        Assert.False(Directory.Exists(sourceAttachmentDirectory));
        Assert.True(Directory.Exists(workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(targetFilePath)));
    }

    [Fact]
    public void DeleteMap_RemovesAttachmentDirectory()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var filePath = workspace.SaveMap("alpha", map);
        var attachmentDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(filePath);
        Directory.CreateDirectory(attachmentDirectory);
        File.WriteAllText(Path.Combine(attachmentDirectory, "capture.png"), "attachment");

        workspace.MapsStorage.DeleteMap(new FileInfo(filePath));

        Assert.False(File.Exists(filePath));
        Assert.False(Directory.Exists(attachmentDirectory));
    }
}
