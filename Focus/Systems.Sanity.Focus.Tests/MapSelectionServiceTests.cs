using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Tests;

public class MapSelectionServiceTests
{
    [Fact]
    public void FindFile_ResolvesByNumberShortcutAndName()
    {
        using var workspace = new TestWorkspace();
        workspace.SaveMap("alpha", new MindMap("Alpha"));
        workspace.SaveMap("beta", new MindMap("Beta"));

        var service = new MapSelectionService(workspace.MapsStorage);
        var selection = service.GetTopSelection();

        var fileByNumber = service.FindFile(selection, "1");
        var fileByShortcut = service.FindFile(selection, AccessibleKeyNumbering.GetStringFor(1));
        var fileByName = service.FindFile(selection, fileByNumber!.NameWithoutExtension());

        Assert.NotNull(fileByNumber);
        Assert.Equal(fileByNumber.FullName, fileByShortcut!.FullName);
        Assert.Equal(fileByNumber.FullName, fileByName!.FullName);
    }
}
