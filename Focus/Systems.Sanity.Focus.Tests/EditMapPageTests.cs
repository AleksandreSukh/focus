using System.IO;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;
using Systems.Sanity.Focus.Pages.Edit;

namespace Systems.Sanity.Focus.Tests;

public class EditMapPageTests
{
    [Fact]
    public void Show_TildePreviewTogglesCommandHelpBeforeExecutingExit()
    {
        using var workspace = new TestWorkspace();
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);
        var commandLineEditor = new PreviewKeyCommandLineEditor(
            new ConsoleKeyInfo('~', ConsoleKey.Oem3, shift: true, alt: false, control: false),
            "exit");

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession(commandLineEditor));
        using var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);

            var page = new EditMapPage(filePath, workspace.AppContext);
            page.Show();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        var renderedOutput = output.ToString();
        var navigateIndex = renderedOutput.IndexOf("Navigate:", StringComparison.InvariantCulture);
        var hiddenHintIndex = renderedOutput.IndexOf("Commands hidden. Press \"~\" to show.", StringComparison.InvariantCulture);

        Assert.True(commandLineEditor.PreviewKeyHandled);
        Assert.True(navigateIndex >= 0);
        Assert.True(hiddenHintIndex > navigateIndex);
    }

    [Fact]
    public void Show_WhenSaveThrows_ShowsGenericSaveErrorAndLogsException()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        var syncHandler = new ThrowingFileSynchronizationHandler
        {
            SynchronizeException = new InvalidOperationException("sync failed")
        };
        using var workspace = new TestWorkspace(fileSynchronizationHandler: syncHandler);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("todo 1", "exit"));
        using var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);

            var page = new EditMapPage(filePath, workspace.AppContext);
            page.Show();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        var renderedOutput = output.ToString();

        Assert.Contains(ExceptionDiagnostics.BuildUserMessage("saving map changes"), renderedOutput);
        Assert.DoesNotContain("sync failed", renderedOutput);
        Assert.Contains("Action: saving map changes", diagnosticsScope.ReadLog());
        Assert.Contains("sync failed", diagnosticsScope.ReadLog());
    }

    [Fact]
    public void Show_WhenSaveHitsUnresolvedGitMerge_ShowsSpecificMergeMessage()
    {
        var syncHandler = new ThrowingFileSynchronizationHandler
        {
            SynchronizeException = new UnresolvedGitMergeException(["alpha.json"])
        };
        using var workspace = new TestWorkspace(fileSynchronizationHandler: syncHandler);
        var map = new MindMap("Root");
        map.AddAtCurrentNode("Child");
        var filePath = workspace.SaveMap("workflow-map", map);

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("todo 1", "exit"));
        using var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);

            var page = new EditMapPage(filePath, workspace.AppContext);
            page.Show();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        var renderedOutput = output.ToString();

        Assert.Contains("Git merge still has unresolved files: alpha.json.", renderedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(ExceptionDiagnostics.BuildUserMessage("saving map changes"), renderedOutput, StringComparison.Ordinal);
    }
}
