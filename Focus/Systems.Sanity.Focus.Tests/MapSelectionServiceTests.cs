using System.Collections.Generic;
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

    [Fact]
    public void FindFile_ResolvesByLocalizedShortcut()
    {
        using var workspace = new TestWorkspace();
        using var translationScope = TranslationTestScope.UseGeorgian();
        workspace.SaveMap("alpha", new MindMap("Alpha"));

        var service = new MapSelectionService(workspace.MapsStorage);
        var selection = service.GetTopSelection();
        var localizedShortcut = AccessibleKeyNumbering.GetStringFor(1).ToLocalLanguage();

        var file = service.FindFile(selection, localizedShortcut);

        Assert.NotNull(file);
        Assert.Equal("alpha", file!.NameWithoutExtension());
    }

    [Fact]
    public void FindFile_ResolvesByLocalizedFileName()
    {
        using var workspace = new TestWorkspace();
        using var translationScope = TranslationTestScope.UseGeorgian();
        workspace.SaveMap("alpha", new MindMap("Alpha"));

        var service = new MapSelectionService(workspace.MapsStorage);
        var selection = service.GetTopSelection();
        var localizedFileName = "alpha".ToLocalLanguage();

        var file = service.FindFile(selection, localizedFileName);

        Assert.NotNull(file);
        Assert.Equal("alpha", file!.NameWithoutExtension());
    }

    [Fact]
    public void FindFile_ResolvesByCapsLockedLocalizedShortcutAndFileName()
    {
        using var workspace = new TestWorkspace();
        using var translationScope = new TranslationTestScope(
            TranslationTestScope.CreateTranslation("caps-home", new Dictionary<string, string>
            {
                ["ä"] = "a",
                ["ł"] = "l",
                ["þ"] = "p",
                ["ħ"] = "h",
                ["ĵ"] = "j"
            }));
        workspace.SaveMap("alpha", new MindMap("Alpha"));

        var service = new MapSelectionService(workspace.MapsStorage);
        var selection = service.GetTopSelection();
        var localizedShortcut = AccessibleKeyNumbering.GetStringFor(1).ToLocalLanguage().ToUpperInvariant();
        var localizedFileName = "alpha".ToLocalLanguage().ToUpperInvariant();

        var fileByShortcut = service.FindFile(selection, localizedShortcut);
        var fileByName = service.FindFile(selection, localizedFileName);

        Assert.NotNull(fileByShortcut);
        Assert.NotNull(fileByName);
        Assert.Equal("alpha", fileByShortcut!.NameWithoutExtension());
        Assert.Equal("alpha", fileByName!.NameWithoutExtension());
    }
}
