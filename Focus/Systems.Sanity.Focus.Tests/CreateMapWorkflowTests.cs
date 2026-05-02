using Systems.Sanity.Focus;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Tests;

public class CreateMapWorkflowTests
{
    [Fact]
    public void Create_SanitizesRequestedFileNameBeforeRequestingPath()
    {
        var interactions = new RecordingWorkflowInteractions();
        using var workspace = new TestWorkspace(workflowInteractions: interactions);

        new CreateMapWorkflow(workspace.AppContext).Create("  [red]Project: Alpha[!]\r\nignored  ", new MindMap("Root"));

        var request = Assert.Single(interactions.RequestedAvailableFilePaths);
        Assert.Equal(workspace.MapsStorage.UserMindMapsDirectory, request.DirectoryPath);
        Assert.Equal("Project_ Alpha", request.FileName);
        Assert.Equal(ConfigurationConstants.RequiredFileNameExtension, request.FileExtension);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\0")]
    public void Create_BlankOrNullCharacterNamesUseUntitled(string requestedFileName)
    {
        var interactions = new RecordingWorkflowInteractions();
        using var workspace = new TestWorkspace(workflowInteractions: interactions);

        new CreateMapWorkflow(workspace.AppContext).Create(requestedFileName, new MindMap("Root"));

        var request = Assert.Single(interactions.RequestedAvailableFilePaths);
        Assert.Equal("untitled", request.FileName);
    }

    [Fact]
    public void Create_WhenPathSelectionIsCancelled_ReturnsNullWithoutSavingOrRefreshingLinkIndex()
    {
        var interactions = new RecordingWorkflowInteractions
        {
            CancelAvailableFilePathRequest = true
        };
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        workspace.AppContext.LinkIndex.QueueLinkSource(new Node("Queued", NodeType.TextItem, 1));

        var createdFilePath = new CreateMapWorkflow(workspace.AppContext).Create("alpha", new MindMap("Alpha"));

        Assert.Null(createdFilePath);
        Assert.Empty(Directory.GetFiles(workspace.MapsStorage.UserMindMapsDirectory, "*.json"));
        Assert.True(workspace.AppContext.LinkIndex.HasQueuedLinkSources);
    }

    [Fact]
    public void Create_SavesMapReturnsPathAndRefreshesLinkIndex()
    {
        var interactions = new RecordingWorkflowInteractions();
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        var map = new MindMap("Alpha");
        var rootIdentifier = map.RootNode.UniqueIdentifier!.Value;
        workspace.AppContext.LinkIndex.QueueLinkSource(new Node("Queued", NodeType.TextItem, 1));

        var createdFilePath = new CreateMapWorkflow(workspace.AppContext).Create("alpha", map);

        Assert.NotNull(createdFilePath);
        Assert.True(File.Exists(createdFilePath));
        Assert.False(workspace.AppContext.LinkIndex.HasQueuedLinkSources);
        Assert.True(workspace.AppContext.LinkIndex.TryGetNode(rootIdentifier, out _));
    }

    [Fact]
    public void Create_UsesInteractionReturnedPathForExistingFileCollisions()
    {
        var interactions = new RecordingWorkflowInteractions();
        using var workspace = new TestWorkspace(workflowInteractions: interactions);
        workspace.SaveMap("alpha", new MindMap("Existing"));
        interactions.AvailableFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "alpha_(2).json");

        var createdFilePath = new CreateMapWorkflow(workspace.AppContext).Create("alpha", new MindMap("Alpha"));

        Assert.Equal(interactions.AvailableFilePath, createdFilePath);
        Assert.True(File.Exists(createdFilePath));
        var request = Assert.Single(interactions.RequestedAvailableFilePaths);
        Assert.Equal("alpha", request.FileName);
    }
}
