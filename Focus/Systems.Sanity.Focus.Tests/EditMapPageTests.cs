using System.IO;
using Systems.Sanity.Focus.Domain;
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
}
