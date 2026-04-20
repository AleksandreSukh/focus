using System.IO;
using System.Text;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Tests;

public class MarkdownExportTests
{
    [Fact]
    public void Export_MarkdownWithAttachments_RendersRootQuotesAndChildImageLinks()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        var child = map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("alpha", map);

        var rootTextAttachment = workspace.AppContext.MapsStorage.AttachmentStore.SaveTextAttachment(
            filePath,
            "First line\nSecond line",
            "Clipboard text.txt");
        var childImageAttachment = workspace.AppContext.MapsStorage.AttachmentStore.SavePngAttachment(
            filePath,
            Encoding.UTF8.GetBytes("fake-png"),
            "Capture.png");

        map.RootNode.AddAttachment(rootTextAttachment);
        child.AddAttachment(childImageAttachment);
        workspace.MapsStorage.SaveMap(filePath, map);

        var exportFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "alpha.md");
        var markdown = MapExportService.Export(
            map.RootNode,
            ExportFormat.Markdown,
            new NodeExportOptions(
                IncludeAttachments: true,
                MapFilePath: filePath,
                ExportFilePath: exportFilePath));

        var expectedImagePath = $"alpha_attachments/{childImageAttachment.RelativePath}";

        Assert.Contains("# Root", markdown);
        Assert.Contains("> First line", markdown);
        Assert.Contains("> Second line", markdown);
        Assert.Contains("1. Child", markdown);
        Assert.Contains($"    [![Capture.png](<{expectedImagePath}>)](<{expectedImagePath}>)", markdown);
    }

    [Fact]
    public void Export_MarkdownWithCollapsedNode_IncludesVisibleNodeAttachmentsButSkipsCollapsedDescendants()
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

        var exportFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "alpha.md");
        var markdown = MapExportService.Export(
            map.RootNode,
            ExportFormat.Markdown,
            new NodeExportOptions(
                SkipCollapsedDescendants: true,
                IncludeAttachments: true,
                MapFilePath: filePath,
                ExportFilePath: exportFilePath));

        Assert.Contains("1. Child", markdown);
        Assert.Contains("    > Attached note", markdown);
        Assert.DoesNotContain("Grandchild", markdown);
    }

    [Fact]
    public void Export_MarkdownUnderHideDoneAncestor_SkipsDoneDescendants()
    {
        var map = new MindMap("Root");
        var branch = map.AddAtCurrentNode("Branch");
        branch.HideDoneTasks = true;
        var subbranch = branch.Add("Subbranch");
        var todoChild = subbranch.Add("Todo child");
        var doneChild = subbranch.Add("Done child");
        todoChild.TaskState = TaskState.Todo;
        doneChild.TaskState = TaskState.Done;

        var markdown = MapExportService.Export(subbranch, ExportFormat.Markdown);

        Assert.Contains("# Subbranch", markdown);
        Assert.Contains("[ ] Todo child", markdown);
        Assert.DoesNotContain("Done child", markdown);
    }

    [Fact]
    public void Export_MarkdownWithMissingAttachment_FallsBackToRelativeLink()
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
        var exportFilePath = Path.Combine(workspace.MapsStorage.UserMindMapsDirectory, "alpha.md");

        var markdown = MapExportService.Export(
            map.RootNode,
            ExportFormat.Markdown,
            new NodeExportOptions(
                IncludeAttachments: true,
                MapFilePath: filePath,
                ExportFilePath: exportFilePath));

        Assert.Contains("[Missing clipboard text.txt](<alpha_attachments/missing.txt>)", markdown);
        Assert.DoesNotContain("> Missing clipboard text.txt", markdown);
    }
}
