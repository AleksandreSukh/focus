using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine.Abstractions;

namespace Systems.Sanity.Focus.Tests;

public class KeyHandlerTests
{
    [Fact]
    public void Handle_LeftArrowThenInsert_WritesAtCursorPosition()
    {
        var handler = new KeyHandler(new FakeConsole(), new List<string>(), autoCompleteHandler: null);

        handler.Handle(Key('a'));
        handler.Handle(Key('c'));
        handler.Handle(Special(ConsoleKey.LeftArrow));
        handler.Handle(Key('b'));

        Assert.Equal("abc", handler.Text);
    }

    [Fact]
    public void Handle_BackspaceAndDelete_RemoveCharacters()
    {
        var handler = new KeyHandler(new FakeConsole(), new List<string>(), autoCompleteHandler: null);

        handler.Handle(Key('a'));
        handler.Handle(Key('b'));
        handler.Handle(Key('c'));
        handler.Handle(Special(ConsoleKey.Backspace));
        handler.Handle(Special(ConsoleKey.LeftArrow));
        handler.Handle(Special(ConsoleKey.Delete));

        Assert.Equal("a", handler.Text);
    }

    [Fact]
    public void Handle_HistoryNavigation_RecallsPreviousEntries()
    {
        var handler = new KeyHandler(new FakeConsole(), new List<string> { "first", "second" }, autoCompleteHandler: null);

        handler.Handle(Special(ConsoleKey.UpArrow));
        Assert.Equal("second", handler.Text);

        handler.Handle(Special(ConsoleKey.UpArrow));
        Assert.Equal("first", handler.Text);

        handler.Handle(Special(ConsoleKey.DownArrow));
        Assert.Equal("second", handler.Text);
    }

    [Fact]
    public void Handle_Tab_CyclesAutoCompleteSuggestions()
    {
        var handler = new KeyHandler(
            new FakeConsole(),
            new List<string>(),
            new FakeAutoCompleteHandler("add", "attach"));

        handler.Handle(Key('a'));
        handler.Handle(Special(ConsoleKey.Tab));
        Assert.Equal("add", handler.Text);

        handler.Handle(Special(ConsoleKey.Tab));
        Assert.Equal("attach", handler.Text);
    }

    [Fact]
    public void Handle_PreviewKeyHandlerCanConsumeInputWithoutWritingCharacter()
    {
        var handler = new KeyHandler(
            new FakeConsole(),
            new List<string>(),
            autoCompleteHandler: null,
            previewKeyHandler: (keyInfo, currentText) => keyInfo.KeyChar == '~' && currentText.Length == 0);

        handler.Handle(Key('~'));

        Assert.Equal(string.Empty, handler.Text);
    }

    [Fact]
    public void Handle_SeededPrintableKeyBehavesLikeNormalTypedInput()
    {
        var handler = new KeyHandler(new FakeConsole(), new List<string>(), autoCompleteHandler: null);

        handler.Handle(Key('x'));
        handler.Handle(Key('y'));

        Assert.Equal("xy", handler.Text);
    }

    private static ConsoleKeyInfo Key(char character) => new(character, 0, false, false, false);

    private static ConsoleKeyInfo Special(ConsoleKey key) => new('\0', key, false, false, false);

    private sealed class FakeAutoCompleteHandler : IAutoCompleteHandler
    {
        private readonly string[] _suggestions;

        public FakeAutoCompleteHandler(params string[] suggestions)
        {
            _suggestions = suggestions;
        }

        public char[] Separators { get; set; } = { ' ' };

        public string[] GetSuggestions(string text, int index)
        {
            return _suggestions;
        }
    }

    private sealed class FakeConsole : IConsole
    {
        public int CursorLeft { get; private set; }

        public int CursorTop { get; private set; }

        public int BufferWidth => 120;

        public int BufferHeight => 120;

        public void SetCursorPosition(int left, int top)
        {
            CursorLeft = left;
            CursorTop = top;
        }

        public void SetBufferSize(int width, int height)
        {
        }

        public void Write(string value)
        {
            foreach (var character in value)
            {
                if (character == '\n')
                {
                    CursorTop++;
                    CursorLeft = 0;
                    continue;
                }

                CursorLeft++;
                if (CursorLeft >= BufferWidth)
                {
                    CursorLeft = 0;
                    CursorTop++;
                }
            }
        }

        public void WriteLine(string value)
        {
            Write(value);
            CursorTop++;
            CursorLeft = 0;
        }
    }
}
