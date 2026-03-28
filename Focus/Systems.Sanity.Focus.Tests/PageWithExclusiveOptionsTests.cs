using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Tests;

public class PageWithExclusiveOptionsTests
{
    [Fact]
    public void GetInput_ForwardsInitialKeyInfoToCommandLineEditor()
    {
        var commandLineEditor = new InitialKeyAwareCommandLineEditor(new[] { "yz" });
        var initialKey = new ConsoleKeyInfo('x', ConsoleKey.X, shift: false, alt: false, control: false);
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession(commandLineEditor));
        var page = new InputForwardingTestPage();

        page.ReadWithInitialKey(initialKey);

        Assert.Equal("xyz", page.EnteredInput!.InputString);
        Assert.Single(commandLineEditor.ReceivedInitialKeys);
        Assert.Equal('x', commandLineEditor.ReceivedInitialKeys[0]!.Value.KeyChar);
    }

    [Fact]
    public void GetCommand_ReplaysPrintableDismissKeyIntoNextPrompt()
    {
        var commandLineEditor = new InitialKeyAwareCommandLineEditor(new[] { "bad", "yz" });
        var dismissKey = new ConsoleKeyInfo('x', ConsoleKey.X, shift: false, alt: false, control: false);
        using var consoleScope = new AppConsoleScope(
            new ScriptedConsoleSession(commandLineEditor, readKeys: new[] { dismissKey }));
        var page = new ExclusiveOptionsTestPage();

        page.Show();

        Assert.Equal("xyz", page.SelectedInput!.InputString);
        Assert.Equal(2, commandLineEditor.ReceivedInitialKeys.Count);
        Assert.Null(commandLineEditor.ReceivedInitialKeys[0]);
        Assert.Equal('x', commandLineEditor.ReceivedInitialKeys[1]!.Value.KeyChar);
    }

    [Fact]
    public void GetCommand_DoesNotReplayNonPrintableDismissKeyIntoNextPrompt()
    {
        var commandLineEditor = new InitialKeyAwareCommandLineEditor(new[] { "bad", "xyz" });
        var dismissKey = new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift: false, alt: false, control: false);
        using var consoleScope = new AppConsoleScope(
            new ScriptedConsoleSession(commandLineEditor, readKeys: new[] { dismissKey }));
        var page = new ExclusiveOptionsTestPage();

        page.Show();

        Assert.Equal("xyz", page.SelectedInput!.InputString);
        Assert.Equal(2, commandLineEditor.ReceivedInitialKeys.Count);
        Assert.Null(commandLineEditor.ReceivedInitialKeys[0]);
        Assert.Null(commandLineEditor.ReceivedInitialKeys[1]);
    }

    private sealed class ExclusiveOptionsTestPage : PageWithExclusiveOptions
    {
        public ConsoleInput? SelectedInput { get; private set; }

        public override void Show()
        {
            SelectedInput = GetCommand();
        }

        protected override string[] GetCommandOptions() => new[] { "xyz" };
    }

    private sealed class InputForwardingTestPage : Page
    {
        public ConsoleInput? EnteredInput { get; private set; }

        public void ReadWithInitialKey(ConsoleKeyInfo initialKeyInfo)
        {
            EnteredInput = GetInput(initialKeyInfo: initialKeyInfo);
        }

        public override void Show()
        {
        }
    }
}
