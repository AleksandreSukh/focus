using System.IO;
using System.Text;
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

    [Fact]
    public void Export_HtmlWithAttachments_RendersRootTextAndImageAttachmentsUsingRelativePaths()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var filePath = workspace.SaveMap("alpha", map);

        var textAttachment = workspace.AppContext.MapsStorage.AttachmentStore.SaveTextAttachment(
            filePath,
            "First line\nSecond line",
            "Clipboard text.txt");
        var imageAttachment = workspace.AppContext.MapsStorage.AttachmentStore.SavePngAttachment(
            filePath,
            Encoding.UTF8.GetBytes("fake-png"),
            "Capture.png");
        map.RootNode.AddAttachment(textAttachment);
        map.RootNode.AddAttachment(imageAttachment);
        workspace.MapsStorage.SaveMap(filePath, map);

        var exportFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "alpha.html");
        var html = MapExportService.Export(
            map.RootNode,
            ExportFormat.Html,
            new NodeExportOptions(
                IncludeAttachments: true,
                MapFilePath: filePath,
                ExportFilePath: exportFilePath));

        var expectedImagePath = $"alpha_attachments/{imageAttachment.RelativePath}";

        Assert.Contains("<blockquote class=\"attachment-quote\">First line", html);
        Assert.Contains("Second line", html);
        Assert.Contains($"href=\"{expectedImagePath}\"", html);
        Assert.Contains($"src=\"{expectedImagePath}\"", html);
        Assert.Contains("class=\"attachment-image\"", html);
        Assert.True(html.IndexOf("<h1>Root</h1>", System.StringComparison.Ordinal) <
                    html.IndexOf("<blockquote class=\"attachment-quote\">First line", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Export_HtmlWithCollapsedNode_IncludesVisibleNodeAttachmentsButSkipsCollapsedDescendants()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var child = map.AddAtCurrentNode("Child");
        child.Collapse();
        child.Add("Grandchild");
        var filePath = workspace.SaveMap("alpha", map);

        var attachment = workspace.AppContext.MapsStorage.AttachmentStore.SaveTextAttachment(
            filePath,
            "Attached note",
            "Clipboard text.txt");
        child.AddAttachment(attachment);
        workspace.MapsStorage.SaveMap(filePath, map);

        var exportFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "alpha.html");
        var html = MapExportService.Export(
            map.RootNode,
            ExportFormat.Html,
            new NodeExportOptions(
                SkipCollapsedDescendants: true,
                IncludeAttachments: true,
                MapFilePath: filePath,
                ExportFilePath: exportFilePath));

        Assert.Contains("Attached note", html);
        Assert.Contains("Child", html);
        Assert.DoesNotContain("Grandchild", html);
    }

    [Fact]
    public void Export_HtmlUnderHideDoneAncestor_SkipsDoneDescendants()
    {
        var map = new MindMap("Root");
        var branch = map.AddAtCurrentNode("Branch");
        branch.HideDoneTasks = true;
        var subbranch = branch.Add("Subbranch");
        var todoChild = subbranch.Add("Todo child");
        var doneChild = subbranch.Add("Done child");
        todoChild.TaskState = TaskState.Todo;
        doneChild.TaskState = TaskState.Done;

        var html = MapExportService.Export(subbranch, ExportFormat.Html);

        Assert.Contains("<h1>Subbranch</h1>", html);
        Assert.Contains("[ ] Todo child", html);
        Assert.DoesNotContain("Done child", html);
    }

    [Fact]
    public void Export_HtmlWithMissingAttachment_FallsBackToRelativeLink()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "missing.txt",
            MediaType = "text/plain; charset=utf-8",
            DisplayName = "Missing clipboard text.txt",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var filePath = workspace.SaveMap("alpha", map);
        var exportFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "alpha.html");

        var html = MapExportService.Export(
            map.RootNode,
            ExportFormat.Html,
            new NodeExportOptions(
                IncludeAttachments: true,
                MapFilePath: filePath,
                ExportFilePath: exportFilePath));

        Assert.Contains("alpha_attachments/missing.txt", html);
        Assert.Contains(">Missing clipboard text.txt</a>", html);
        Assert.DoesNotContain("<blockquote class=\"attachment-quote\">", html);
        Assert.DoesNotContain("<img class=\"attachment-image\"", html);
    }
}
