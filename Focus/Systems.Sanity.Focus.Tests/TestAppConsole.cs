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
    private readonly Queue<ConsoleKeyInfo> _readKeys;
    private readonly int _windowWidth;

    public ScriptedConsoleSession(params string[] inputs)
        : this(new ScriptedCommandLineEditor(inputs), windowWidth: 120)
    {
    }

    public ScriptedConsoleSession(int windowWidth, params string[] inputs)
        : this(new ScriptedCommandLineEditor(inputs), windowWidth)
    {
    }

    public ScriptedConsoleSession(
        ICommandLineEditor commandLineEditor,
        int windowWidth = 120,
        IEnumerable<ConsoleKeyInfo>? readKeys = null)
    {
        _windowWidth = windowWidth;
        _readKeys = new Queue<ConsoleKeyInfo>(readKeys ?? Array.Empty<ConsoleKeyInfo>());
        CommandLineEditor = commandLineEditor;
    }

    public ICommandLineEditor CommandLineEditor { get; }

    public int WindowWidth => _windowWidth;

    public List<string> BackgroundMessages { get; } = new();

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

    public void WriteBackgroundMessage(string text)
    {
        BackgroundMessages.Add(text);
    }

    public ConsoleKeyInfo ReadKey(bool intercept = true) =>
        _readKeys.Count > 0
            ? _readKeys.Dequeue()
            : new('\r', ConsoleKey.Enter, shift: false, alt: false, control: false);

    public void Beep()
    {
    }
}

internal sealed class RecordingPageNavigator : IPageNavigator
{
    public string? OpenedEditMapFilePath { get; private set; }

    public Guid? OpenedEditMapNodeIdentifier { get; private set; }

    public string? OpenedCreateMapFileName { get; private set; }

    public string? OpenedCreateMapSourceFilePath { get; private set; }

    public void OpenCreateMap(string fileName, MindMap mindMap, string? sourceMapFilePath = null)
    {
        OpenedCreateMapFileName = fileName;
        OpenedCreateMapSourceFilePath = sourceMapFilePath;
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

    public void WriteInterleavedMessage(string text)
    {
    }

    public string Read(
        string prompt,
        string defaultInput = "",
        Action<string>? beforeEachAutoCompleteSuggestionWrite = null,
        Action<string>? afterEachAutoCompleteSuggestionWrite = null,
        Func<ConsoleKeyInfo, string, bool>? previewKeyHandler = null,
        ConsoleKeyInfo? initialKeyInfo = null)
    {
        return _inputs.Count > 0
            ? _inputs.Dequeue()
            : string.Empty;
    }

    public IReadOnlyList<string> GetHistory() => Array.Empty<string>();
}

internal sealed class PreviewKeyCommandLineEditor : ICommandLineEditor
{
    private readonly ConsoleKeyInfo _previewKeyInfo;
    private readonly string _currentText;
    private readonly string _input;

    public PreviewKeyCommandLineEditor(ConsoleKeyInfo previewKeyInfo, string input, string currentText = "")
    {
        _previewKeyInfo = previewKeyInfo;
        _currentText = currentText;
        _input = input;
    }

    public bool HistoryEnabled { get; set; }

    public bool PreviewKeyHandled { get; private set; }

    public void SetAutoCompletionHandler(IAutoCompleteHandler handler)
    {
    }

    public void WriteInterleavedMessage(string text)
    {
    }

    public string Read(
        string prompt,
        string defaultInput = "",
        Action<string>? beforeEachAutoCompleteSuggestionWrite = null,
        Action<string>? afterEachAutoCompleteSuggestionWrite = null,
        Func<ConsoleKeyInfo, string, bool>? previewKeyHandler = null,
        ConsoleKeyInfo? initialKeyInfo = null)
    {
        PreviewKeyHandled = previewKeyHandler?.Invoke(_previewKeyInfo, _currentText) == true;
        return _input;
    }

    public IReadOnlyList<string> GetHistory() => Array.Empty<string>();
}

internal sealed class FakeClipboardCaptureService : IClipboardCaptureService
{
    private readonly ClipboardCaptureResult _result;

    public FakeClipboardCaptureService(ClipboardCaptureResult result)
    {
        _result = result;
    }

    public int CaptureCallCount { get; private set; }

    public ClipboardCaptureResult Capture()
    {
        CaptureCallCount++;
        return _result;
    }
}

internal sealed class RecordingFileOpener : IFileOpener
{
    public string? OpenedFilePath { get; private set; }

    public string? ErrorMessage { get; set; }

    public bool TryOpen(string filePath, out string? errorMessage)
    {
        OpenedFilePath = filePath;
        errorMessage = ErrorMessage;
        return string.IsNullOrWhiteSpace(ErrorMessage);
    }
}

internal sealed class RecordingClipboardCommandRunner : IClipboardCommandRunner
{
    private readonly Queue<Func<string, IReadOnlyList<string>, string?, ClipboardCommandResult>> _responses = new();

    public List<(string FileName, IReadOnlyList<string> Arguments, string? OutputFilePath)> Calls { get; } = new();

    public void EnqueueResponse(Func<string, IReadOnlyList<string>, string?, ClipboardCommandResult> response)
    {
        _responses.Enqueue(response);
    }

    public ClipboardCommandResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        Calls.Add((fileName, arguments, null));
        return _responses.Dequeue().Invoke(fileName, arguments, null);
    }

    public ClipboardCommandResult RunToFile(string fileName, IReadOnlyList<string> arguments, string outputFilePath)
    {
        Calls.Add((fileName, arguments, outputFilePath));
        return _responses.Dequeue().Invoke(fileName, arguments, outputFilePath);
    }
}

internal sealed class InitialKeyAwareCommandLineEditor : ICommandLineEditor
{
    private readonly Queue<string> _typedInputSuffixes;

    public InitialKeyAwareCommandLineEditor(IEnumerable<string> typedInputSuffixes)
    {
        _typedInputSuffixes = new Queue<string>(typedInputSuffixes);
    }

    public bool HistoryEnabled { get; set; }

    public List<ConsoleKeyInfo?> ReceivedInitialKeys { get; } = new();

    public void SetAutoCompletionHandler(IAutoCompleteHandler handler)
    {
    }

    public void WriteInterleavedMessage(string text)
    {
    }

    public string Read(
        string prompt,
        string defaultInput = "",
        Action<string>? beforeEachAutoCompleteSuggestionWrite = null,
        Action<string>? afterEachAutoCompleteSuggestionWrite = null,
        Func<ConsoleKeyInfo, string, bool>? previewKeyHandler = null,
        ConsoleKeyInfo? initialKeyInfo = null)
    {
        ReceivedInitialKeys.Add(initialKeyInfo);
        var typedSuffix = _typedInputSuffixes.Count > 0
            ? _typedInputSuffixes.Dequeue()
            : string.Empty;

        return initialKeyInfo.HasValue
            ? $"{initialKeyInfo.Value.KeyChar}{typedSuffix}"
            : typedSuffix;
    }

    public IReadOnlyList<string> GetHistory() => Array.Empty<string>();
}
