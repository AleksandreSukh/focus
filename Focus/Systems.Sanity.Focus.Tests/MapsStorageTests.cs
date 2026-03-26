using Systems.Sanity.Focus.Domain;

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
}
