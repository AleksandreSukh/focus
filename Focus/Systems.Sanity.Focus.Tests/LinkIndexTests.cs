using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Tests;

public class LinkIndexTests
{
    [Fact]
    public void Rebuild_TracksOutgoingLinksAndBacklinksAcrossMaps()
    {
        using var workspace = new TestWorkspace();

        var sourceMap = new MindMap("Source");
        sourceMap.AddAtCurrentNode("Source child");
        Assert.True(sourceMap.ChangeCurrentNode("1"));
        var sourceNode = sourceMap.GetCurrentNode();
        sourceMap.GoToRoot();

        var targetMap = new MindMap("Target");
        targetMap.AddAtCurrentNode("Target child");
        Assert.True(targetMap.ChangeCurrentNode("1"));
        var targetNode = targetMap.GetCurrentNode();
        targetMap.GoToRoot();

        sourceNode.AddLink(targetNode);

        workspace.SaveMap("source", sourceMap);
        workspace.SaveMap("target", targetMap);

        var linkIndex = new LinkIndex();
        linkIndex.Rebuild(workspace.MapsStorage);
        var navigation = new LinkNavigationService(linkIndex);

        var outgoingLinks = navigation.GetOutgoingLinks(sourceNode);
        var backlinks = navigation.GetBacklinks(targetNode);

        Assert.Single(outgoingLinks);
        Assert.Equal("Target child", outgoingLinks[0].NodeName);
        Assert.Single(backlinks);
        Assert.Equal("Source child", backlinks[0].NodeName);
    }
}
