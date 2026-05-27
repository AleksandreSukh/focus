using Newtonsoft.Json.Linq;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Application.EditCommands;
using Systems.Sanity.Focus.Application.Llm;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Tests;

public class LlmInteropTests
{
    [Fact]
    public void PromptDetection_RequiresOpenAiTaskNode()
    {
        var prompt = new Node("@ai Summarize", NodeType.TextItem, 1)
        {
            TaskState = TaskState.Todo
        };
        var doingPrompt = new Node("@AI Draft answer", NodeType.TextItem, 2)
        {
            TaskState = TaskState.Doing
        };
        var donePrompt = new Node("@ai Already answered", NodeType.TextItem, 3)
        {
            TaskState = TaskState.Done
        };
        var ideaPrompt = new Node("@ai Idea", NodeType.IdeaBagItem, 4)
        {
            TaskState = TaskState.Todo
        };

        Assert.True(LlmPromptService.IsPromptNode(prompt));
        Assert.True(LlmPromptService.IsPromptNode(doingPrompt));
        Assert.False(LlmPromptService.IsPromptNode(donePrompt));
        Assert.False(LlmPromptService.IsPromptNode(ideaPrompt));
        Assert.False(LlmPromptService.IsPromptNode(new Node("Regular task", NodeType.TextItem, 5)
        {
            TaskState = TaskState.Todo
        }));
    }

    [Fact]
    public void ContextBuilder_IncludesTreeLinksBacklinksAndUrls()
    {
        using var workspace = new TestWorkspace();
        var selectedMap = new MindMap("Alpha");
        var promptNode = selectedMap.AddAtCurrentNode("@ai Summarize https://example.com/root");
        promptNode.TaskState = TaskState.Todo;
        Assert.True(selectedMap.ChangeCurrentNode("1"));
        selectedMap.AddAtCurrentNode("Detail https://example.com/detail");
        selectedMap.GoToRoot();

        var targetMap = new MindMap("Target");
        var targetNode = targetMap.AddAtCurrentNode("Target child");
        promptNode.AddLink(targetNode, LinkRelationType.Prerequisite);

        var backlinkMap = new MindMap("Backlink");
        var backlinkNode = backlinkMap.AddAtCurrentNode("Backlink source");
        backlinkNode.AddLink(promptNode, LinkRelationType.Causes);

        var alphaPath = workspace.SaveMap("Alpha", selectedMap);
        workspace.SaveMap("Target", targetMap);
        workspace.SaveMap("Backlink", backlinkMap);

        var context = new LlmContextBuilder().Build(
            workspace.AppContext,
            selectedMap,
            alphaPath,
            promptNode.UniqueIdentifier!.Value);
        var markdown = new LlmContextBuilder().ToMarkdown(context!);
        var json = JObject.Parse(new LlmContextBuilder().ToJson(context!));

        Assert.NotNull(context);
        Assert.Equal("Summarize https://example.com/root", context!.Prompt.Text);
        Assert.Equal("Alpha > @ai Summarize https://example.com/root", context.Prompt.Path);
        Assert.Equal("Detail https://example.com/detail", context.Subtree.Children.Single().Name);
        Assert.Equal("prerequisite", context.Links.Outgoing.Single().RelationLabel);
        Assert.Equal("Target child", context.Links.Outgoing.Single().NodeName);
        Assert.Equal("backlink: causes", context.Links.Backlinks.Single().RelationLabel);
        Assert.Equal("Backlink source", context.Links.Backlinks.Single().NodeName);
        Assert.Contains(context.Urls, url => url.Url == "https://example.com/root");
        Assert.Contains(context.Urls, url => url.Url == "https://example.com/detail");
        Assert.Contains("## Backlinks", markdown);
        Assert.Equal("subtree-links", json["mode"]!.Value<string>());
    }

    [Fact]
    public void JobStore_CreatesClaimsListsAndResolvesProtocolPaths()
    {
        using var workspace = new TestWorkspace();
        var mapPath = workspace.SaveMap("Alpha", new MindMap("Alpha"));
        var store = new LlmJobStore(workspace.MapsStorage.UserMindMapsDirectory);
        var nodeId = Guid.NewGuid();

        var entry = store.CreateJob(mapPath, nodeId, "Summarize");
        var claimed = store.Claim(entry, "Codex");
        var jobs = store.ListJobs();

        Assert.True(File.Exists(entry.FilePath));
        Assert.EndsWith(Path.Combine("_llm", "jobs", $"{entry.Job.Id}.json"), entry.FilePath);
        Assert.Equal("FocusMaps/Alpha.json", entry.Job.MapFilePath);
        Assert.Single(jobs);
        Assert.Equal(LlmJobStatus.Claimed, jobs.Single().Job.Status);
        Assert.Equal("Codex", claimed.Job.ClaimedBy);
        Assert.Equal(mapPath, store.ResolveMapPath("FocusMaps/Alpha.json"));
        Assert.Equal(mapPath, store.ResolveMapPath("Alpha.json"));
        Assert.Equal(mapPath, store.ResolveMapPath(mapPath));
    }

    [Fact]
    public void CodexRunner_ResolvesWindowsCommandShimFromPath()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "focus-codex-path", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var executableName = OperatingSystem.IsWindows() ? "codex.cmd" : "codex";
            var executablePath = Path.Combine(tempDirectory, executableName);
            File.WriteAllText(executablePath, string.Empty);

            var resolved = CodexLlmAgentClient.ResolveExecutablePath("codex", tempDirectory);
            var processCommand = CodexLlmAgentClient.BuildProcessCommand("codex", tempDirectory);

            Assert.Equal(executablePath, resolved, ignoreCase: OperatingSystem.IsWindows());
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal("cmd.exe", processCommand.FileName);
                Assert.Contains(processCommand.PrefixArguments, argument =>
                    string.Equals(argument, executablePath, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                Assert.Equal(executablePath, processCommand.FileName);
                Assert.Empty(processCommand.PrefixArguments);
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ExecuteAiPrompt_CreatesPromptRunsCodexAndAppendsAnswer()
    {
        var agent = new RecordingLlmAgentClient(LlmAgentResponse.Success("Answer line 1\nAnswer line 2"));
        var statusSink = new RecordingApplicationStatusSink();
        using var workspace = new TestWorkspace(llmAgentClient: agent, statusSink: statusSink);
        var filePath = workspace.SaveMap("workflow-map", new MindMap("Root"));
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("ai Summarize this branch"));
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var promptNode = reopened.RootNode.Children.Single();
        var answerNode = promptNode.Children.Single();
        var job = new LlmJobStore(workspace.MapsStorage.UserMindMapsDirectory).ListJobs().Single().Job;

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("Answer AI prompt in workflow-map", result.SyncCommitMessage);
        Assert.Equal("@ai Summarize this branch", promptNode.Name);
        Assert.Equal(TaskState.Done, promptNode.TaskState);
        Assert.Equal(NodeType.TextBlockItem, answerNode.NodeType);
        Assert.Equal("Answer line 1\nAnswer line 2", answerNode.Name.ReplaceLineEndings("\n"));
        Assert.Equal("llm:Codex", answerNode.Metadata!.Source);
        Assert.Equal("Codex", answerNode.Metadata.Device);
        Assert.Equal(LlmJobStatus.Completed, job.Status);
        Assert.Contains("Summarize this branch", agent.Requests.Single().Prompt);
        Assert.Contains("## Tree", agent.Requests.Single().ContextMarkdown);
        Assert.Equal(["Waiting for Codex..."], statusSink.BusyMessages);
        Assert.Equal(1, statusSink.BusyDisposeCount);
    }

    [Fact]
    public void ExecuteAiOnRegularChild_UsesChildTextAsPrompt()
    {
        var agent = new RecordingLlmAgentClient(LlmAgentResponse.Success("Answer for existing node"));
        using var workspace = new TestWorkspace(llmAgentClient: agent);
        var map = new MindMap("Root");
        var promptSource = map.AddAtCurrentNode("ja Summarize the existing node");
        promptSource.TaskState = TaskState.Todo;
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("ai ja"));
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var sourceNode = reopened.RootNode.Children.Single();
        var answerNode = sourceNode.Children.Single();
        var job = new LlmJobStore(workspace.MapsStorage.UserMindMapsDirectory).ListJobs().Single().Job;

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("Answer AI prompt in workflow-map", result.SyncCommitMessage);
        Assert.Equal("ja Summarize the existing node", sourceNode.Name);
        Assert.Equal(TaskState.Done, sourceNode.TaskState);
        Assert.Equal(NodeType.TextBlockItem, answerNode.NodeType);
        Assert.Equal("Answer for existing node", answerNode.Name);
        Assert.Equal("ja Summarize the existing node", job.Prompt);
        Assert.Equal("ja Summarize the existing node", agent.Requests.Single().Prompt);
    }

    [Fact]
    public void ExecuteAiWithUnmatchedText_StillCreatesPromptNode()
    {
        var agent = new RecordingLlmAgentClient(LlmAgentResponse.Success("Unmatched answer"));
        using var workspace = new TestWorkspace(llmAgentClient: agent);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Other child");
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("ai Summarize unmatched prompt"));
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var promptNode = reopened.RootNode.Children.Single(node => node.Name == "@ai Summarize unmatched prompt");

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("Other child", reopened.RootNode.Children.First().Name);
        Assert.Equal(TaskState.Done, promptNode.TaskState);
        Assert.Equal("Unmatched answer", promptNode.Children.Single().Name);
        Assert.Equal("Summarize unmatched prompt", agent.Requests.Single().Prompt);
    }

    [Fact]
    public void ExecuteAiOnChildPrompt_ProcessesExistingPrompt()
    {
        var agent = new RecordingLlmAgentClient(LlmAgentResponse.Success("Child prompt answer"));
        using var workspace = new TestWorkspace(llmAgentClient: agent);
        var map = new MindMap("Root");
        var prompt = map.AddAtCurrentNode("@ai Explain child prompt");
        prompt.TaskState = TaskState.Todo;
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("ai 1"));
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var promptNode = reopened.RootNode.Children.Single();

        Assert.True(result.IsSuccess);
        Assert.True(result.ShouldPersist);
        Assert.Equal("@ai Explain child prompt", promptNode.Name);
        Assert.Equal(TaskState.Done, promptNode.TaskState);
        Assert.Equal("Child prompt answer", promptNode.Children.Single().Name);
        Assert.Equal("Explain child prompt", agent.Requests.Single().Prompt);
    }

    [Fact]
    public void ExecuteAiOnBlankChild_ReturnsEmptyPromptError()
    {
        var agent = new RecordingLlmAgentClient(LlmAgentResponse.Success("Should not run"));
        using var workspace = new TestWorkspace(llmAgentClient: agent);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("   ");
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var result = workflow.Execute(new ConsoleInput("ai 1"));
        var jobs = new LlmJobStore(workspace.MapsStorage.UserMindMapsDirectory).ListJobs();

        Assert.False(result.IsSuccess);
        Assert.False(result.ShouldPersist);
        Assert.Equal("AI prompt is empty.", result.ErrorString);
        Assert.Empty(jobs);
        Assert.Empty(agent.Requests);
    }

    [Fact]
    public void ExecuteAiOnCurrentPrompt_ProcessesExistingPrompt()
    {
        var agent = new RecordingLlmAgentClient(LlmAgentResponse.Success("Existing prompt answer"));
        using var workspace = new TestWorkspace(llmAgentClient: agent);
        var map = new MindMap("Root");
        var prompt = map.AddAtCurrentNode("@ai Explain current node");
        prompt.TaskState = TaskState.Todo;
        var filePath = workspace.SaveMap("workflow-map", map);
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        Assert.True(workflow.Execute(new ConsoleInput("cd 1")).IsSuccess);
        var result = workflow.Execute(new ConsoleInput("ai"));
        workflow.Save(result.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskState.Done, reopened.RootNode.Children.Single().TaskState);
        Assert.Equal("Existing prompt answer", reopened.RootNode.Children.Single().Children.Single().Name);
    }

    [Fact]
    public void ExecuteAiJobs_ListsAndRunsPendingJob()
    {
        var agent = new RecordingLlmAgentClient(LlmAgentResponse.Success("Queued answer"));
        using var workspace = new TestWorkspace(llmAgentClient: agent);
        var map = new MindMap("Root");
        var prompt = map.AddAtCurrentNode("@ai Process queued job");
        prompt.TaskState = TaskState.Todo;
        var filePath = workspace.SaveMap("workflow-map", map);
        var store = new LlmJobStore(workspace.MapsStorage.UserMindMapsDirectory);
        var job = store.CreateJob(filePath, prompt.UniqueIdentifier!.Value, "Process queued job").Job;
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        var listResult = workflow.Execute(new ConsoleInput("aijobs"));
        var runResult = workflow.Execute(new ConsoleInput("aijobs run"));
        workflow.Save(runResult.SyncCommitMessage!);
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var completedJob = store.ListJobs().Single().Job;

        Assert.True(listResult.IsSuccess);
        Assert.Contains(job.Id, listResult.Message);
        Assert.True(runResult.IsSuccess);
        Assert.Equal("Queued answer", reopened.RootNode.Children.Single().Children.Single().Name);
        Assert.Equal(LlmJobStatus.Completed, completedJob.Status);
    }

    [Fact]
    public void ExecuteAi_WhenCodexFails_FailsJobWithoutAppendingAnswer()
    {
        var agent = new RecordingLlmAgentClient(LlmAgentResponse.Error("codex missing"));
        using var workspace = new TestWorkspace(llmAgentClient: agent);
        var map = new MindMap("Root");
        var prompt = map.AddAtCurrentNode("@ai Explain current node");
        prompt.TaskState = TaskState.Todo;
        var filePath = workspace.SaveMap("workflow-map", map);
        var store = new LlmJobStore(workspace.MapsStorage.UserMindMapsDirectory);
        store.CreateJob(filePath, prompt.UniqueIdentifier!.Value, "Explain current node");
        var workflow = new EditWorkflow(filePath, workspace.AppContext);

        Assert.True(workflow.Execute(new ConsoleInput("cd 1")).IsSuccess);
        var result = workflow.Execute(new ConsoleInput("ai"));
        var reopened = workspace.MapsStorage.OpenMap(filePath);
        var failedJob = store.ListJobs().Single().Job;

        Assert.False(result.IsSuccess);
        Assert.Contains("codex missing", result.ErrorString);
        Assert.Empty(reopened.RootNode.Children.Single().Children);
        Assert.Equal(TaskState.Todo, reopened.RootNode.Children.Single().TaskState);
        Assert.Equal(LlmJobStatus.Failed, failedJob.Status);
        Assert.Equal("codex missing", failedJob.ErrorMessage);
    }

    private sealed class RecordingLlmAgentClient : ILlmAgentClient
    {
        private readonly Queue<LlmAgentResponse> _responses;

        public RecordingLlmAgentClient(params LlmAgentResponse[] responses)
        {
            _responses = new Queue<LlmAgentResponse>(responses);
        }

        public List<LlmAgentRequest> Requests { get; } = new();

        public LlmAgentResponse Run(LlmAgentRequest request)
        {
            Requests.Add(request);
            return _responses.Count > 0
                ? _responses.Dequeue()
                : LlmAgentResponse.Success("Default fake answer");
        }
    }
}
