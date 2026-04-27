using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;
using Systems.Sanity.Focus.Pages.Edit;

namespace Systems.Sanity.Focus.Tests;

public class SyncCommitMessagePipelineTests
{
    [Fact]
    public void Sync_ForwardsCommitMessageToSynchronizationHandler()
    {
        using var workspace = new TestWorkspace();
        var handler = new RecordingFileSynchronizationHandler();
        var mapsStorage = CreateMapsStorage(workspace.RootDirectory, handler);

        mapsStorage.Sync("Custom sync message");

        Assert.Equal(["Custom sync message"], handler.CommitMessages);
    }

    [Fact]
    public void Execute_ExportMarkdown_UsesFormatSpecificSyncCommitMessage()
    {
        using var workspace = new TestWorkspace();
        var handler = new RecordingFileSynchronizationHandler();
        var mapsStorage = CreateMapsStorage(workspace.RootDirectory, handler);
        var appContext = new FocusAppContext(mapsStorage);
        var filePath = mapsStorage.SaveMapToStorage("alpha", new MindMap("Alpha"));
        var workflow = new EditWorkflow(filePath, appContext);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("save"));
        var result = workflow.Execute(new ConsoleInput("export"));

        Assert.True(result.IsSuccess);
        Assert.Equal(["Export markdown from alpha"], handler.CommitMessages);
    }

    [Fact]
    public void Execute_ExportHtml_UsesFormatSpecificSyncCommitMessage()
    {
        using var workspace = new TestWorkspace();
        var handler = new RecordingFileSynchronizationHandler();
        var mapsStorage = CreateMapsStorage(workspace.RootDirectory, handler);
        var appContext = new FocusAppContext(mapsStorage);
        var filePath = mapsStorage.SaveMapToStorage("alpha", new MindMap("Alpha"));
        var workflow = new EditWorkflow(filePath, appContext);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("html", "save"));
        var result = workflow.Execute(new ConsoleInput("export"));

        Assert.True(result.IsSuccess);
        Assert.Equal(["Export HTML from alpha"], handler.CommitMessages);
    }

    [Fact]
    public void Execute_ExportHtmlWithBlackBackground_WritesDarkHtmlAndKeepsCommitMessage()
    {
        using var workspace = new TestWorkspace();
        var handler = new RecordingFileSynchronizationHandler();
        var mapsStorage = CreateMapsStorage(workspace.RootDirectory, handler);
        var appContext = new FocusAppContext(mapsStorage);
        var filePath = mapsStorage.SaveMapToStorage("alpha", new MindMap("Alpha"));
        var workflow = new EditWorkflow(filePath, appContext);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("html", "blackbg", "save"));
        var result = workflow.Execute(new ConsoleInput("export"));
        var exportedHtmlPath = Directory.GetFiles(mapsStorage.UserMindMapsDirectory, "*.html").Single();
        var exportedHtml = File.ReadAllText(exportedHtmlPath);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Export HTML from alpha"], handler.CommitMessages);
        Assert.Contains(":root { color-scheme: dark; }", exportedHtml);
        Assert.Contains("background: #000000;", exportedHtml);
    }

    [Fact]
    public void Execute_ExportCopyText_CopiesPlainTextWithoutFileOrSync()
    {
        using var workspace = new TestWorkspace();
        var handler = new RecordingFileSynchronizationHandler();
        var mapsStorage = CreateMapsStorage(workspace.RootDirectory, handler);
        var clipboardTextWriter = new FakeClipboardTextWriter();
        var appContext = new FocusAppContext(
            mapsStorage,
            navigator: null,
            clipboardTextWriter: clipboardTextWriter);
        var map = new MindMap("Alpha");
        var child = map.AddAtCurrentNode("Child");
        child.TaskState = TaskState.Todo;
        var filePath = mapsStorage.SaveMapToStorage("alpha", map);
        var workflow = new EditWorkflow(filePath, appContext);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("copytext"));
        var result = workflow.Execute(new ConsoleInput("export"));

        Assert.True(result.IsSuccess);
        Assert.False(result.ShouldPersist);
        Assert.Empty(handler.CommitMessages);
        Assert.Equal("Copied plain text export to clipboard", result.Message);
        Assert.Equal("Alpha\n- [ ] Child\n", clipboardTextWriter.CopiedTexts.Single().ReplaceLineEndings("\n"));
        Assert.Empty(Directory.GetFiles(mapsStorage.UserMindMapsDirectory, "*.md"));
        Assert.Empty(Directory.GetFiles(mapsStorage.UserMindMapsDirectory, "*.html"));
        Assert.Empty(Directory.GetFiles(mapsStorage.UserMindMapsDirectory, "*.txt"));
    }

    [Fact]
    public void Show_PersistedCommand_ForwardsGeneratedSyncCommitMessageToHandler()
    {
        using var workspace = new TestWorkspace();
        var handler = new RecordingFileSynchronizationHandler();
        var mapsStorage = CreateMapsStorage(workspace.RootDirectory, handler);
        var appContext = new FocusAppContext(mapsStorage);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = mapsStorage.SaveMapToStorage("alpha", map);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("min 1", "exit"));
        new EditMapPage(filePath, appContext).Show();

        Assert.Equal(["Hide node in alpha"], handler.CommitMessages);
    }

    private static MapsStorage CreateMapsStorage(string dataFolder, RecordingFileSynchronizationHandler handler)
    {
        return new MapsStorage(
            new UserConfig
            {
                DataFolder = dataFolder,
                GitRepository = string.Empty
            },
            handler);
    }

    private sealed class RecordingFileSynchronizationHandler : IFileSynchronizationHandler
    {
        public List<string> CommitMessages { get; } = new();

        public void Synchronize(string commitMessage)
        {
            CommitMessages.Add(commitMessage);
        }

        public StartupSyncResult PullLatestAtStartup()
        {
            return StartupSyncResult.Skipped;
        }

        public MergeRecoveryResult TryRecoverResolvedFile(string absoluteFilePath)
        {
            return MergeRecoveryResult.NoAction;
        }
    }
}

internal static class SyncCommitMessagePipelineTestExtensions
{
    public static string SaveMapToStorage(this MapsStorage mapsStorage, string fileName, MindMap map)
    {
        var normalizedFileName = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.json";
        var filePath = Path.Combine(mapsStorage.UserMindMapsDirectory, normalizedFileName);
        mapsStorage.SaveMap(filePath, map);
        return filePath;
    }
}
