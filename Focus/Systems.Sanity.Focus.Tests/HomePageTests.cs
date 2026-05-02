using System.IO;
using System.Reflection;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Tests;

public class HomePageTests
{
    [Fact]
    public void Show_TildePreviewTogglesCommandHelpBeforeExecutingExit()
    {
        using var workspace = new TestWorkspace();
        var commandLineEditor = new PreviewKeyCommandLineEditor(
            CommandHelpVisibilityState.BuildToggleKeyInfo(),
            "exit");

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession(commandLineEditor));
        using var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);

            var page = new HomePage(workspace.AppContext);
            page.Show();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        var renderedOutput = output.ToString();
        var hiddenHintIndex = renderedOutput.IndexOf(CommandHelpText.HiddenHelpMessage, StringComparison.InvariantCulture);
        var commandHelpIndex = renderedOutput.IndexOf("new <file name>", StringComparison.InvariantCulture);

        Assert.True(commandLineEditor.PreviewKeyHandled);
        Assert.True(hiddenHintIndex >= 0);
        Assert.True(commandHelpIndex > hiddenHintIndex);
    }

    [Fact]
    public void Show_LocalizedFileNameInput_OpensSelectedMap()
    {
        var navigator = new RecordingPageNavigator();
        using var workspace = new TestWorkspace(navigator);
        using var translationScope = TranslationTestScope.UseGeorgian();
        var filePath = workspace.SaveMap("alpha", new MindMap("Alpha"));
        var localizedFileName = "alpha".ToLocalLanguage();

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession(localizedFileName, "exit"));
        var page = new HomePage(workspace.AppContext);

        page.Show();

        Assert.Equal(filePath, navigator.OpenedEditMapFilePath);
    }

    [Fact]
    public void Show_UppercaseExitInput_IsAcceptedWithoutWrongInputDialog()
    {
        using var workspace = new TestWorkspace();
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("EXIT", "exit"));
        using var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);

            var page = new HomePage(workspace.AppContext);
            page.Show();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        Assert.DoesNotContain("*** Wrong Input ***", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Show_WhenHomePageLoopThrows_ShowsGenericErrorAndLogsException()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        using var workspace = new TestWorkspace();
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession(
            new ThrowingCommandLineEditor(
                new InvalidOperationException("home page input failed"),
                "exit")));
        using var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);

            var page = new HomePage(workspace.AppContext);
            page.Show();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        var renderedOutput = output.ToString();

        Assert.Contains(ExceptionDiagnostics.BuildUserMessage("executing home page command"), renderedOutput);
        Assert.DoesNotContain("home page input failed", renderedOutput);
        Assert.Contains("Action: executing home page command", diagnosticsScope.ReadLog());
        Assert.Contains("home page input failed", diagnosticsScope.ReadLog());
    }

    [Fact]
    public void GetSuggestions_WithFiles_IncludesOpenShortcuts()
    {
        using var workspace = new TestWorkspace();
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession());
        workspace.SaveMap("alpha", new MindMap("Alpha"));
        var page = new HomePage(workspace.AppContext);
        typeof(HomePage)
            .GetField("_fileSelection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, new HomeWorkflow(workspace.AppContext).GetFileSelection());

        var suggestions = page.GetSuggestions(string.Empty, 0);

        Assert.Contains("1", suggestions);
        Assert.Contains(AccessibleKeyNumbering.GetStringFor(1), suggestions);
    }
}
