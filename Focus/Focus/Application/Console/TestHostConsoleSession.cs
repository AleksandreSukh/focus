#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;
using SysConsole = System.Console;

namespace Systems.Sanity.Focus.Application.Console;

internal sealed class TestHostConsoleSession : IConsoleAppSession
{
    private const string EmitTitlesEnvironmentVariable = "FOCUS_TEST_EMIT_TITLES";
    private readonly TestHostPipeTransport _transport;
    private readonly bool _emitTitles;

    public TestHostConsoleSession(string pipeName)
    {
        _transport = new TestHostPipeTransport(pipeName);
        _emitTitles = string.Equals(
            Environment.GetEnvironmentVariable(EmitTitlesEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
        CommandLineEditor = new TestHostCommandLineEditor(_transport);
    }

    public ICommandLineEditor CommandLineEditor { get; }

    public int WindowWidth => _transport.WindowWidth;

    public void SetTitle(string? title)
    {
        if (_emitTitles && !string.IsNullOrWhiteSpace(title))
            SysConsole.WriteLine($":title> {title}");
    }

    public void Clear()
    {
    }

    public void ClearScrollback()
    {
    }

    public void Write(string text)
    {
        SysConsole.Write(text);
    }

    public void WriteLine(string text)
    {
        SysConsole.WriteLine(text);
    }

    public void WriteBackgroundMessage(string text)
    {
        CommandLineEditor.WriteInterleavedMessage(text);
    }

    public ConsoleKeyInfo ReadKey(bool intercept = true)
    {
        var response = _transport.RequestKey(intercept);
        var keyChar = string.IsNullOrEmpty(response.KeyChar)
            ? default
            : response.KeyChar[0];
        var key = Enum.TryParse<ConsoleKey>(response.Key, ignoreCase: true, out var parsedKey)
            ? parsedKey
            : ConsoleKey.NoName;

        return new ConsoleKeyInfo(
            keyChar,
            key,
            response.Shift,
            response.Alt,
            response.Control);
    }

    public void Beep()
    {
    }

    private sealed class TestHostCommandLineEditor : ICommandLineEditor
    {
        private readonly List<string> _history = new();
        private readonly TestHostPipeTransport _transport;

        public TestHostCommandLineEditor(TestHostPipeTransport transport)
        {
            _transport = transport;
        }

        public bool HistoryEnabled { get; set; }

        public void SetAutoCompletionHandler(IAutoCompleteHandler handler)
        {
        }

        public void WriteInterleavedMessage(string text)
        {
            SysConsole.WriteLine(text);
        }

        public string Read(
            string prompt,
            string defaultInput = "",
            Action<string>? beforeEachAutoCompleteSuggestionWrite = null,
            Action<string>? afterEachAutoCompleteSuggestionWrite = null,
            Func<ConsoleKeyInfo, string, bool>? previewKeyHandler = null,
            ConsoleKeyInfo? initialKeyInfo = null)
        {
            if (!string.IsNullOrEmpty(prompt))
                SysConsole.Write(prompt);

            var currentText = initialKeyInfo.HasValue && IsPrintable(initialKeyInfo.Value.KeyChar)
                ? initialKeyInfo.Value.KeyChar.ToString()
                : string.Empty;
            while (true)
            {
                var response = _transport.RequestLine(prompt, defaultInput, currentText);
                if (string.Equals(response.Type, "key", StringComparison.Ordinal))
                {
                    var keyInfo = ToConsoleKeyInfo(response);
                    if (previewKeyHandler?.Invoke(keyInfo, currentText) == true)
                        continue;

                    if (keyInfo.Key == ConsoleKey.Backspace && currentText.Length > 0)
                    {
                        currentText = currentText[..^1];
                        continue;
                    }

                    if (IsPrintable(keyInfo.KeyChar))
                        currentText += keyInfo.KeyChar;

                    continue;
                }

                var typedText = response.Text ?? string.Empty;
                if (!string.IsNullOrEmpty(currentText))
                    typedText = $"{currentText}{typedText}";

                EchoTypedInput(typedText, response.EchoDelayMs);

                if (HistoryEnabled && !string.IsNullOrWhiteSpace(typedText))
                    _history.Add(typedText);

                return typedText;
            }
        }

        public string ReadMultiline(string prompt, string defaultInput = "")
        {
            return MultilineInputCollector.Read(
                linePrompt => Read(linePrompt),
                prompt,
                defaultInput);
        }

        public IReadOnlyList<string> GetHistory() => _history;

        private static ConsoleKeyInfo ToConsoleKeyInfo(TestHostResponseMessage response)
        {
            var keyChar = string.IsNullOrEmpty(response.KeyChar)
                ? default
                : response.KeyChar[0];
            var key = Enum.TryParse<ConsoleKey>(response.Key, ignoreCase: true, out var parsedKey)
                ? parsedKey
                : ConsoleKey.NoName;

            return new ConsoleKeyInfo(
                keyChar,
                key,
                response.Shift,
                response.Alt,
                response.Control);
        }

        private static void EchoTypedInput(string input, int echoDelayMs)
        {
            if (echoDelayMs <= 0)
            {
                SysConsole.WriteLine(input);
                return;
            }

            foreach (var character in input)
            {
                SysConsole.Write(character);
                Thread.Sleep(echoDelayMs);
            }

            SysConsole.WriteLine();
        }

        private static bool IsPrintable(char keyChar) =>
            keyChar != default &&
            !char.IsControl(keyChar);
    }

    private sealed class TestHostPipeTransport
    {
        private const int DefaultWindowWidth = 120;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly NamedPipeServerStream _pipeServer;
        private readonly SemaphoreSlim _requestLock = new(1, 1);
        private readonly object _connectionLock = new();
        private readonly Task _connectionTask;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        public TestHostPipeTransport(string pipeName)
        {
            _pipeServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            _connectionTask = _pipeServer.WaitForConnectionAsync();
        }

        public int WindowWidth { get; private set; } = DefaultWindowWidth;

        public TestHostResponseMessage RequestLine(string prompt, string defaultInput, string initialText)
        {
            return Exchange(
                new TestHostRequestMessage
                {
                    Type = "request-line",
                    Prompt = prompt,
                    DefaultInput = defaultInput,
                    InitialText = initialText
                },
                expectedResponseTypes: ["line", "key"]);
        }

        public TestHostResponseMessage RequestKey(bool intercept)
        {
            return Exchange(
                new TestHostRequestMessage
                {
                    Type = "request-key",
                    Intercept = intercept
                },
                "key");
        }

        private TestHostResponseMessage Exchange(TestHostRequestMessage request, params string[] expectedResponseTypes)
        {
            _requestLock.Wait();
            try
            {
                EnsureConnected();
                WriteMessage(request);
                var response = ReadMessage();
                if (!expectedResponseTypes.Any(expectedResponseType =>
                        string.Equals(response.Type, expectedResponseType, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Expected test-host response \"{string.Join("\" or \"", expectedResponseTypes)}\" but received \"{response.Type}\".");
                }

                return response;
            }
            finally
            {
                _requestLock.Release();
            }
        }

        private void EnsureConnected()
        {
            if (_reader != null && _writer != null)
                return;

            lock (_connectionLock)
            {
                if (_reader != null && _writer != null)
                    return;

                _connectionTask.GetAwaiter().GetResult();
                _reader = new StreamReader(_pipeServer, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                _writer = new StreamWriter(_pipeServer, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
                {
                    AutoFlush = true
                };

                var hello = ReadMessage();
                if (!string.Equals(hello.Type, "hello", StringComparison.Ordinal))
                    throw new InvalidOperationException("Test host handshake failed. Expected hello message.");

                if (hello.WindowWidth > 0)
                    WindowWidth = hello.WindowWidth;
            }
        }

        private TestHostResponseMessage ReadMessage()
        {
            var line = _reader?.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                throw new EndOfStreamException("Test host disconnected unexpectedly.");

            return JsonSerializer.Deserialize<TestHostResponseMessage>(line, JsonOptions)
                   ?? throw new InvalidOperationException("Failed to deserialize test-host message.");
        }

        private void WriteMessage(TestHostRequestMessage message)
        {
            var serialized = JsonSerializer.Serialize(message, JsonOptions);
            _writer?.WriteLine(serialized);
        }
    }

    private sealed class TestHostRequestMessage
    {
        public string? Type { get; set; }

        public string? Prompt { get; set; }

        public string? DefaultInput { get; set; }

        public string? InitialText { get; set; }

        public bool Intercept { get; set; }
    }

    private sealed class TestHostResponseMessage
    {
        public string? Type { get; set; }

        public string? Text { get; set; }

        public int EchoDelayMs { get; set; }

        public string? Key { get; set; }

        public string? KeyChar { get; set; }

        public bool Shift { get; set; }

        public bool Alt { get; set; }

        public bool Control { get; set; }

        public int WindowWidth { get; set; }
    }
}
