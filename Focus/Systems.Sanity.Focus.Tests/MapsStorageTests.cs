using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;
using Newtonsoft.Json.Linq;
using Systems.Sanity.Focus.Application;

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
    public void SaveAndOpenMap_RoundTripsExplicitShowDoneOverride()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.RootNode.HideDoneTasks = true;
        map.RootNode.HideDoneTasksExplicit = true;
        var child = map.AddAtCurrentNode("Branch");
        child.HideDoneTasks = false;
        child.HideDoneTasksExplicit = true;

        var filePath = workspace.SaveMap("alpha", map);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var reopenedChild = reopened.GetNode("1");

        Assert.True(reopened.RootNode.HideDoneTasks);
        Assert.True(reopened.RootNode.HideDoneTasksExplicit);
        Assert.NotNull(reopenedChild);
        Assert.False(reopenedChild!.HideDoneTasks);
        Assert.True(reopenedChild.HideDoneTasksExplicit);
    }

    [Fact]
    public void SaveAndOpenMap_RoundTripsStarredFlag()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        Assert.True(map.StarNode("1", out _));

        var filePath = workspace.SaveMap("alpha", map);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var reopenedChild = reopened.GetNode("1");

        Assert.NotNull(reopenedChild);
        Assert.True(reopenedChild!.Starred);
        Assert.Contains("\"starred\": true", File.ReadAllText(filePath), StringComparison.Ordinal);
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
    public void OpenMapForEditing_OpensNormalMapWithoutRewriting()
    {
        var syncHandler = new RecordingFileSynchronizationHandler();
        using var workspace = new TestWorkspace(fileSynchronizationHandler: syncHandler);
        var filePath = workspace.SaveMap("alpha", new MindMap("Root"));
        var originalJson = File.ReadAllText(filePath);
        syncHandler.RecoveredFilePaths.Clear();

        var reopened = workspace.MapsStorage.OpenMapForEditing(filePath);

        Assert.Equal("Root", reopened.RootNode.Name);
        Assert.Equal(originalJson, File.ReadAllText(filePath));
        Assert.Equal([filePath], syncHandler.RecoveredFilePaths);
    }

    [Fact]
    public void OpenMapForEditing_ResolvesConflictedJsonAndRewritesFile()
    {
        var syncHandler = new RecordingFileSynchronizationHandler();
        using var workspace = new TestWorkspace(fileSynchronizationHandler: syncHandler);
        var filePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "conflicted.json");
        File.WriteAllText(
            filePath,
            BuildWholeDocumentConflict(
                BuildMapJson("Root ours", "2026-04-20T08:00:00Z", "2026-04-20T08:00:00Z"),
                BuildMapJson("Root theirs", "2026-04-20T10:00:00Z", "2026-04-20T10:00:00Z")));

        var reopened = workspace.MapsStorage.OpenMapForEditing(filePath);
        var resolvedJson = File.ReadAllText(filePath);

        Assert.Equal("Root theirs", reopened.RootNode.Name);
        Assert.DoesNotContain("<<<<<<<", resolvedJson, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"Root theirs\"", resolvedJson, StringComparison.Ordinal);
        Assert.Equal([filePath], syncHandler.RecoveredFilePaths);
    }

    [Fact]
    public void OpenMapForEditing_WhenConflictCannotBeResolved_ThrowsDedicatedException()
    {
        using var workspace = new TestWorkspace();
        var filePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "conflicted.json");
        const string conflictedContent = "<<<<<<< HEAD\nnot json\n=======\nstill not json\n>>>>>>> abc123";
        File.WriteAllText(filePath, conflictedContent);

        var exception = Assert.Throws<MapConflictAutoResolveException>(() => workspace.MapsStorage.OpenMapForEditing(filePath));

        Assert.Equal(MapConflictAutoResolveException.DefaultMessage, exception.Message);
        Assert.Equal(conflictedContent, File.ReadAllText(filePath));
    }

    [Fact]
    public void SaveMap_AttemptsMergeRecoveryAfterWritingFile()
    {
        var syncHandler = new RecordingFileSynchronizationHandler();
        using var workspace = new TestWorkspace(fileSynchronizationHandler: syncHandler);
        var filePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "alpha.json");

        workspace.MapsStorage.SaveMap(filePath, new MindMap("Root"));

        Assert.Equal([filePath], syncHandler.RecoveredFilePaths);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void MoveMap_PreservesNodeScopedAttachmentDirectory()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var sourceFilePath = workspace.SaveMap("alpha", map);
        var sourceAttachmentDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(
            sourceFilePath,
            GetRequiredNodeIdentifier(map.RootNode));
        Directory.CreateDirectory(sourceAttachmentDirectory);
        File.WriteAllText(Path.Combine(sourceAttachmentDirectory, "capture.png"), "attachment");

        var targetFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "beta.json");

        workspace.MapsStorage.MoveMap(sourceFilePath, targetFilePath);

        Assert.True(Directory.Exists(sourceAttachmentDirectory));
        Assert.Equal(
            sourceAttachmentDirectory,
            workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(
                targetFilePath,
                GetRequiredNodeIdentifier(map.RootNode)));
    }

    [Fact]
    public void OpenMap_MigratesLegacyAttachmentDirectoryToNodeScopedLayout()
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

        var filePath = workspace.SaveMap("alpha", map);
        var legacyAttachmentPath = workspace.MapsStorage.AttachmentStore.ResolveLegacyAttachmentPath(
            filePath,
            "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyAttachmentPath)!);
        File.WriteAllText(legacyAttachmentPath, "attachment");

        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var migratedAttachmentPath = workspace.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            filePath,
            GetRequiredNodeIdentifier(reopened.RootNode),
            "capture.png");

        Assert.True(File.Exists(migratedAttachmentPath));
        Assert.False(File.Exists(legacyAttachmentPath));
        Assert.False(Directory.Exists(workspace.MapsStorage.AttachmentStore.GetLegacyAttachmentDirectoryPath(filePath)));
    }

    [Fact]
    public void SaveMap_MigratesLegacyAttachmentDirectoryToNodeScopedLayout()
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

        var filePath = workspace.SaveMap("alpha", map);
        var legacyAttachmentPath = workspace.MapsStorage.AttachmentStore.ResolveLegacyAttachmentPath(
            filePath,
            "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyAttachmentPath)!);
        File.WriteAllText(legacyAttachmentPath, "attachment");

        workspace.MapsStorage.SaveMap(filePath, map);

        var migratedAttachmentPath = workspace.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            filePath,
            GetRequiredNodeIdentifier(map.RootNode),
            "capture.png");
        Assert.True(File.Exists(migratedAttachmentPath));
        Assert.False(File.Exists(legacyAttachmentPath));
        Assert.False(Directory.Exists(workspace.MapsStorage.AttachmentStore.GetLegacyAttachmentDirectoryPath(filePath)));
    }

    [Fact]
    public void MoveMap_RenamesLegacyAttachmentDirectoryWhenPresent()
    {
        using var workspace = new TestWorkspace();
        var sourceFilePath = workspace.SaveMap("alpha", new MindMap("Root"));
        var legacyAttachmentDirectory = workspace.MapsStorage.AttachmentStore.GetLegacyAttachmentDirectoryPath(sourceFilePath);
        Directory.CreateDirectory(legacyAttachmentDirectory);
        File.WriteAllText(Path.Combine(legacyAttachmentDirectory, "capture.png"), "attachment");

        var targetFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "beta.json");

        workspace.MapsStorage.MoveMap(sourceFilePath, targetFilePath);

        Assert.False(Directory.Exists(legacyAttachmentDirectory));
        Assert.True(Directory.Exists(workspace.MapsStorage.AttachmentStore.GetLegacyAttachmentDirectoryPath(targetFilePath)));
        Assert.True(File.Exists(Path.Combine(
            workspace.MapsStorage.AttachmentStore.GetLegacyAttachmentDirectoryPath(targetFilePath),
            "capture.png")));
    }

    [Fact]
    public void DeleteMap_DeletesNodeScopedAttachmentDirectoriesAndPrunesEmptyRoot()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var filePath = workspace.SaveMap("alpha", map);
        var attachmentDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(
            filePath,
            GetRequiredNodeIdentifier(map.RootNode));
        var attachmentRootDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentRootDirectoryPath(filePath);
        Directory.CreateDirectory(attachmentDirectory);
        File.WriteAllText(Path.Combine(attachmentDirectory, "capture.png"), "attachment");

        workspace.MapsStorage.DeleteMap(new FileInfo(filePath));

        Assert.False(File.Exists(filePath));
        Assert.False(Directory.Exists(attachmentDirectory));
        Assert.False(Directory.Exists(attachmentRootDirectory));
    }

    [Fact]
    public void DeleteMap_PreserveAttachmentsMode_DoesNotDeleteNodeScopedAttachmentDirectory()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var filePath = workspace.SaveMap("alpha", map);
        var attachmentDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(
            filePath,
            GetRequiredNodeIdentifier(map.RootNode));
        Directory.CreateDirectory(attachmentDirectory);
        File.WriteAllText(Path.Combine(attachmentDirectory, "capture.png"), "attachment");

        workspace.MapsStorage.DeleteMap(new FileInfo(filePath), MapDeletionMode.PreserveAttachments);

        Assert.False(File.Exists(filePath));
        Assert.True(Directory.Exists(attachmentDirectory));
    }

    [Fact]
    public void DeleteMap_DeletesLegacyAttachmentDirectoryWhenPresent()
    {
        using var workspace = new TestWorkspace();
        var filePath = workspace.SaveMap("alpha", new MindMap("Root"));
        var legacyAttachmentDirectory = workspace.MapsStorage.AttachmentStore.GetLegacyAttachmentDirectoryPath(filePath);
        Directory.CreateDirectory(legacyAttachmentDirectory);
        File.WriteAllText(Path.Combine(legacyAttachmentDirectory, "capture.png"), "attachment");

        workspace.MapsStorage.DeleteMap(new FileInfo(filePath));

        Assert.False(File.Exists(filePath));
        Assert.False(Directory.Exists(legacyAttachmentDirectory));
    }

    [Fact]
    public void DeleteMap_DeletesOnlyCurrentMapsNodeScopedAttachmentDirectories()
    {
        using var workspace = new TestWorkspace();
        var alphaMap = new MindMap("Alpha");
        var alphaFilePath = workspace.SaveMap("alpha", alphaMap);
        var alphaAttachmentDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(
            alphaFilePath,
            GetRequiredNodeIdentifier(alphaMap.RootNode));
        Directory.CreateDirectory(alphaAttachmentDirectory);
        File.WriteAllText(Path.Combine(alphaAttachmentDirectory, "alpha.png"), "attachment");

        var betaMap = new MindMap("Beta");
        var betaFilePath = workspace.SaveMap("beta", betaMap);
        var betaAttachmentDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(
            betaFilePath,
            GetRequiredNodeIdentifier(betaMap.RootNode));
        Directory.CreateDirectory(betaAttachmentDirectory);
        File.WriteAllText(Path.Combine(betaAttachmentDirectory, "beta.png"), "attachment");

        workspace.MapsStorage.DeleteMap(new FileInfo(alphaFilePath));

        Assert.False(File.Exists(alphaFilePath));
        Assert.False(Directory.Exists(alphaAttachmentDirectory));
        Assert.True(File.Exists(betaFilePath));
        Assert.True(Directory.Exists(betaAttachmentDirectory));
    }

    [Fact]
    public void DeleteMap_WhenMapIsUnreadable_ThrowsAndLeavesMapAndAttachmentsUntouched()
    {
        using var workspace = new TestWorkspace();
        var filePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "broken.json");
        File.WriteAllText(filePath, "{ \"rootNode\": ");

        var nodeIdentifier = Guid.NewGuid();
        var attachmentDirectory = workspace.MapsStorage.AttachmentStore.GetAttachmentDirectoryPath(filePath, nodeIdentifier);
        Directory.CreateDirectory(attachmentDirectory);
        File.WriteAllText(Path.Combine(attachmentDirectory, "capture.png"), "attachment");

        var exception = Assert.Throws<MapDeletionBlockedException>(
            () => workspace.MapsStorage.DeleteMap(new FileInfo(filePath)));

        Assert.Equal(
            "Map \"broken.json\" cannot be deleted because it is unreadable and its attachment folders cannot be determined safely.",
            exception.Message);
        Assert.True(File.Exists(filePath));
        Assert.True(Directory.Exists(attachmentDirectory));
    }

    [Fact]
    public void SaveMap_WhenDuplicateNodeIdentifierIsRepaired_MovesNodeAttachmentDirectory()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var child = map.AddAtCurrentNode("Child");
        var duplicateIdentifier = GetRequiredNodeIdentifier(map.RootNode);
        child.UniqueIdentifier = duplicateIdentifier;
        child.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.png",
            MediaType = "image/png",
            DisplayName = "Capture.png",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var filePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "alpha.json");
        var sourceAttachmentPath = workspace.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            filePath,
            duplicateIdentifier,
            "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceAttachmentPath)!);
        File.WriteAllText(sourceAttachmentPath, "attachment");

        workspace.MapsStorage.SaveMap(filePath, map);

        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var reopenedChild = reopened.GetNode("1");
        Assert.NotNull(reopenedChild);
        var remappedIdentifier = GetRequiredNodeIdentifier(reopenedChild!);
        var remappedAttachmentPath = workspace.MapsStorage.AttachmentStore.ResolveAttachmentPath(
            filePath,
            remappedIdentifier,
            "capture.png");

        Assert.NotEqual(duplicateIdentifier, remappedIdentifier);
        Assert.False(File.Exists(sourceAttachmentPath));
        Assert.True(File.Exists(remappedAttachmentPath));
    }

    private static string BuildWholeDocumentConflict(string ours, string theirs) =>
        $"<<<<<<< HEAD\n{ours}\n=======\n{theirs}\n>>>>>>> abc123";

    private static string BuildMapJson(string rootName, string rootUpdatedAtUtc, string updatedAt) =>
        $$"""
        {
          "rootNode": {
            "nodeType": 0,
            "uniqueIdentifier": "11111111-1111-1111-1111-111111111111",
            "name": "{{rootName}}",
            "children": [],
            "links": {},
            "number": 1,
            "collapsed": false,
            "hideDoneTasks": false,
            "taskState": 0,
            "metadata": {
              "createdAtUtc": "2026-04-20T07:00:00Z",
              "updatedAtUtc": "{{rootUpdatedAtUtc}}",
              "source": "manual",
              "device": "focus-pwa-web",
              "attachments": []
            }
          },
          "updatedAt": "{{updatedAt}}"
        }
        """;

    private sealed class RecordingFileSynchronizationHandler : IFileSynchronizationHandler
    {
        public List<string> RecoveredFilePaths { get; } = new();

        public void Synchronize(string commitMessage)
        {
        }

        public StartupSyncResult PullLatestAtStartup()
        {
            return StartupSyncResult.Skipped;
        }

        public MergeRecoveryResult TryRecoverResolvedFile(string absoluteFilePath)
        {
            RecoveredFilePaths.Add(absoluteFilePath);
            return MergeRecoveryResult.NoAction;
        }
    }

    private static Guid GetRequiredNodeIdentifier(Node node) =>
        node.UniqueIdentifier ?? throw new InvalidOperationException("Node identifier is required for attachment tests.");
}
