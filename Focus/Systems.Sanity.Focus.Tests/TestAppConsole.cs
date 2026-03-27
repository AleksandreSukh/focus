using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Application.Console;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;

namespace Systems.Sanity.Focus.Tests;

internal sealed class AppConsoleScope : IDisposable
{
    private readonly IConsoleAppSession _previousSession;

    public AppConsoleScope(IConsoleAppSession replacementSession)
    {
        _previousSession = AppConsole.Current;
        AppConsole.Current = replacementSession;
    }

    public void Dispose()
    {
        AppConsole.Current = _previousSession;
    }
}

internal sealed class ScriptedConsoleSession : IConsoleAppSession
{
    public ScriptedConsoleSession(params string[] inputs)
    {
        CommandLineEditor = new ScriptedCommandLineEditor(inputs);
    }

    public ICommandLineEditor CommandLineEditor { get; }

    public int WindowWidth => 120;

    public void SetTitle(string? title)
    {
    }

    public void Clear()
    {
    }

    public void ClearScrollback()
    {
    }

    public void Write(string text)
    {
    }

    public void WriteLine(string text)
    {
    }

    public ConsoleKeyInfo ReadKey(bool intercept = true) =>
        new('\r', ConsoleKey.Enter, shift: false, alt: false, control: false);

    public void Beep()
    {
    }
}

internal sealed class RecordingPageNavigator : IPageNavigator
{
    public string? OpenedEditMapFilePath { get; private set; }

    public Guid? OpenedEditMapNodeIdentifier { get; private set; }

    public void OpenCreateMap(string fileName, MindMap mindMap)
    {
    }

    public void OpenEditMap(string filePath, Guid? initialNodeIdentifier = null)
    {
        OpenedEditMapFilePath = filePath;
        OpenedEditMapNodeIdentifier = initialNodeIdentifier;
    }
}

internal sealed class ScriptedCommandLineEditor : ICommandLineEditor
{
    private readonly Queue<string> _inputs;

    public ScriptedCommandLineEditor(IEnumerable<string> inputs)
    {
        _inputs = new Queue<string>(inputs);
    }

    public bool HistoryEnabled { get; set; }

    public void SetAutoCompletionHandler(IAutoCompleteHandler handler)
    {
    }

    public string Read(
        string prompt,
        string defaultInput = "",
        Action<string>? beforeEachAutoCompleteSuggestionWrite = null,
        Action<string>? afterEachAutoCompleteSuggestionWrite = null)
    {
        return _inputs.Count > 0
            ? _inputs.Dequeue()
            : string.Empty;
    }

    public IReadOnlyList<string> GetHistory() => Array.Empty<string>();
}
