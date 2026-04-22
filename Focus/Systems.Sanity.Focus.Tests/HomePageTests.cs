using System.IO;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages;

namespace Systems.Sanity.Focus.Tests;

public class HomePageTests
{
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
}
