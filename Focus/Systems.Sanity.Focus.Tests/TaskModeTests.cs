using Newtonsoft.Json.Linq;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Tests;

public class TaskModeTests
{
    [Fact]
    public void GetTasks_ExcludesIdeaTagsAndSortsByPriority()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Todo task");
        map.AddAtCurrentNode("Doing task");
        map.AddAtCurrentNode("Done task");
        var ideaTag = map.AddIdeaAtCurrentNode("Idea tag");
        Assert.True(map.SetTaskState("1", TaskState.Todo, out _));
        Assert.True(map.SetTaskState("2", TaskState.Doing, out _));
        Assert.True(map.SetTaskState("3", TaskState.Done, out _));
        ideaTag.TaskState = TaskState.Todo;

        var filePath = workspace.SaveMap("tasks", map);
        var results = TaskQueryService.GetTasks(map, filePath, TaskListFilter.All);

        Assert.Collection(
            results,
            result => Assert.Equal("Doing task", result.NodeName),
            result => Assert.Equal("Todo task", result.NodeName),
            result => Assert.Equal("Done task", result.NodeName));
    }

    [Fact]
    public void SearchAndTaskResults_RenderConsistentTaskMarkers()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Write docs");
        Assert.True(map.SetTaskState("1", TaskState.Todo, out _));
        var filePath = workspace.SaveMap("search", map);

        var searchResult = MindMapSearchService.Search(map, "Write", filePath).Single();
        var taskResult = TaskQueryService.GetTasks(map, filePath, TaskListFilter.Todo).Single();

        Assert.Equal("[ ] Root > Write docs", searchResult.ToDisplayString(includeMapName: false));
        Assert.Equal(searchResult.ToDisplayString(includeMapName: false), taskResult.ToDisplayString(includeMapName: false));
    }

    [Fact]
    public void Export_PreservesTaskMarkers()
    {
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Ship feature");
        Assert.True(map.SetTaskState("1", TaskState.Doing, out _));

        var markdown = MapExportService.Export(map.RootNode, ExportFormat.Markdown);
        var html = MapExportService.Export(map.RootNode, ExportFormat.Html);

        Assert.Contains("1. [~] Ship feature", markdown);
        Assert.Contains("[~] Ship feature", html);
    }

    [Fact]
    public void OpenMap_WhenTaskStateMissing_LoadsAsNoneAndRoundTrips()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("legacy-task-map", map);

        var json = JObject.Parse(File.ReadAllText(filePath));
        foreach (var property in json.Descendants().OfType<JProperty>().Where(property => property.Name == "taskState").ToArray())
        {
            property.Remove();
        }

        File.WriteAllText(filePath, json.ToString());

        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var reopenedChild = reopened.GetNode("1");

        Assert.NotNull(reopenedChild);
        Assert.Equal(TaskState.None, reopenedChild!.TaskState);

        workspace.MapsStorage.SaveMap(filePath, reopened);

        var roundTripped = workspace.MapsStorage.OpenMap(filePath);
        Assert.Equal(TaskState.None, roundTripped.GetNode("1")!.TaskState);
    }
}
