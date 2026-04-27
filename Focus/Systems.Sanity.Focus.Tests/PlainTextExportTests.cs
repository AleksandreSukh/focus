using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Tests;

public class PlainTextExportTests
{
    [Fact]
    public void Export_PlainText_RendersChatFriendlyTree()
    {
        var map = new MindMap("[red]Root[!]");
        var todo = map.AddAtCurrentNode("[cyan]Todo child[!]");
        todo.TaskState = TaskState.Todo;
        todo.Add("Grandchild");
        map.AddBlockAtCurrentNode("Block title\n\nBlock body");

        var plainText = Normalize(MapExportService.Export(map.RootNode, ExportFormat.PlainText));

        Assert.Equal(
            "Root\n" +
            "- [ ] Todo child\n" +
            "  - Grandchild\n" +
            "- Block title\n" +
            "  > Block title\n" +
            "  > \n" +
            "  > Block body\n",
            plainText);
    }

    [Fact]
    public void Export_PlainTextWithCollapsedNode_CanSkipCollapsedDescendants()
    {
        var map = new MindMap("Root");
        var child = map.AddAtCurrentNode("Child");
        child.Collapse();
        child.Add("Grandchild");

        var plainText = Normalize(MapExportService.Export(
            map.RootNode,
            ExportFormat.PlainText,
            new NodeExportOptions(SkipCollapsedDescendants: true)));

        Assert.Contains("- Child", plainText);
        Assert.DoesNotContain("Grandchild", plainText);
    }

    [Fact]
    public void Export_PlainTextUnderHideDoneAncestor_SkipsDoneDescendants()
    {
        var map = new MindMap("Root");
        var branch = map.AddAtCurrentNode("Branch");
        branch.HideDoneTasks = true;
        var todoChild = branch.Add("Todo child");
        var doneChild = branch.Add("Done child");
        todoChild.TaskState = TaskState.Todo;
        doneChild.TaskState = TaskState.Done;

        var plainText = Normalize(MapExportService.Export(branch, ExportFormat.PlainText));

        Assert.Contains("- [ ] Todo child", plainText);
        Assert.DoesNotContain("Done child", plainText);
    }

    [Fact]
    public void Export_PlainText_OmitsAttachments()
    {
        var map = new MindMap("Root");
        map.RootNode.AddAttachment(new NodeAttachment
        {
            Id = Guid.NewGuid(),
            RelativePath = "capture.txt",
            MediaType = "text/plain; charset=utf-8",
            DisplayName = "Clipboard attachment.txt",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var plainText = Normalize(MapExportService.Export(
            map.RootNode,
            ExportFormat.PlainText,
            new NodeExportOptions(IncludeAttachments: true)));

        Assert.Equal("Root\n", plainText);
        Assert.DoesNotContain("Clipboard attachment", plainText);
    }

    private static string Normalize(string value) =>
        value.ReplaceLineEndings("\n");
}
