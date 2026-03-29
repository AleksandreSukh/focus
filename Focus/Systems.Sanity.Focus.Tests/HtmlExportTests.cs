using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Tests;

public class HtmlExportTests
{
    [Fact]
    public void Export_DefaultHtml_UsesExistingLightThemeStyles()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Ship feature");

        var html = MapExportService.Export(map.RootNode, ExportFormat.Html);

        Assert.Contains(":root { color-scheme: light; }", html);
        Assert.Contains("background: #ffffff;", html);
        Assert.Contains("color: #1f2328;", html);
        Assert.Contains(".mindmap-export a, .mindmap-export a:visited { color: inherit;", html);
    }

    [Fact]
    public void Export_BlackBackgroundHtml_UsesDarkThemeStylesAndPreservesContent()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Ship feature");
        Assert.True(map.SetTaskState("1", TaskState.Doing, out _));

        var html = MapExportService.Export(
            map.RootNode,
            ExportFormat.Html,
            new NodeExportOptions(UseBlackBackground: true));

        Assert.Contains(":root { color-scheme: dark; }", html);
        Assert.Contains("background: #000000;", html);
        Assert.Contains("color: #f5f7fa;", html);
        Assert.Contains(".mindmap-export a, .mindmap-export a:visited { color: #7dd3fc;", html);
        Assert.Contains(".color-black { color: #f5f7fa; }", html);
        Assert.Contains("<article class=\"mindmap-export\">", html);
        Assert.Contains("<ol>", html);
        Assert.Contains("[~] Ship feature", html);
    }
}
