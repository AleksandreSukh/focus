using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Systems.Sanity.Focus.E2E.Tests;

internal sealed class FocusTestHostClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _pipeName;
    private readonly int _windowWidth;
    private readonly Channel<QueuedResponse> _queuedResponses = Channel.CreateUnbounded<QueuedResponse>();
    private readonly CancellationTokenSource _cancellation = new();

    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Task? _requestProcessorTask;
    private Exception? _backgroundFailure;

    public FocusTestHostClient(string pipeName, int windowWidth = 120)
    {
        _pipeName = pipeName;
        _windowWidth = windowWidth;
    }

    public async Task ConnectAsync(TimeSpan? timeout = null)
    {
        var deadlineUtc = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (true)
        {
            ThrowIfFaulted();

            _pipeClient?.Dispose();
            _pipeClient = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            try
            {
                await _pipeClient.ConnectAsync(250, _cancellation.Token);
                break;
            }
            catch (TimeoutException) when (DateTime.UtcNow < deadlineUtc)
            {
                _pipeClient.Dispose();
                _pipeClient = null;
            }
        }

        _reader = new StreamReader(_pipeClient!, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        _writer = new StreamWriter(_pipeClient!, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 1024, leaveOpen: true)
        {
            AutoFlush = true
        };

        await WriteMessageAsync(new TestHostResponseMessage
        {
            Type = "hello",
            WindowWidth = _windowWidth
        });
        _requestProcessorTask = Task.Run(ProcessRequestsAsync);
    }

    public Task SendLineAsync(string text, int echoDelayMs = 0)
    {
        ThrowIfFaulted();
        return QueueResponseAsync(new TestHostResponseMessage
        {
            Type = "line",
            Text = text,
            EchoDelayMs = echoDelayMs
        });
    }

    public Task SendKeyAsync(ConsoleKeyInfo keyInfo)
    {
        ThrowIfFaulted();
        return QueueResponseAsync(new TestHostResponseMessage
        {
            Type = "key",
            Key = keyInfo.Key.ToString(),
            KeyChar = keyInfo.KeyChar == default ? string.Empty : keyInfo.KeyChar.ToString(),
            Shift = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
            Alt = (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
            Control = (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
        });
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        _queuedResponses.Writer.TryComplete();

        if (_requestProcessorTask != null)
        {
            try
            {
                await _requestProcessorTask;
            }
            catch
            {
            }
        }

        _writer?.Dispose();
        _reader?.Dispose();
        _pipeClient?.Dispose();
        _cancellation.Dispose();
    }

    private async Task QueueResponseAsync(TestHostResponseMessage message)
    {
        var queuedResponse = new QueuedResponse(message);
        await _queuedResponses.Writer.WriteAsync(queuedResponse, _cancellation.Token);
        await queuedResponse.Consumed.Task.WaitAsync(TimeSpan.FromSeconds(15), _cancellation.Token);
    }

    private async Task ProcessRequestsAsync()
    {
        try
        {
            while (!_cancellation.IsCancellationRequested)
            {
                var request = await ReadMessageAsync();
                var queuedResponse = await _queuedResponses.Reader.ReadAsync(_cancellation.Token);
                EnsureResponseMatchesRequest(request, queuedResponse.Message);
                await WriteMessageAsync(queuedResponse.Message);
                queuedResponse.Consumed.TrySetResult();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _backgroundFailure = ex;
            _queuedResponses.Writer.TryComplete(ex);
        }
    }

    private async Task<TestHostRequestMessage> ReadMessageAsync()
    {
        var line = await _reader!.ReadLineAsync(_cancellation.Token);
        if (string.IsNullOrWhiteSpace(line))
            throw new EndOfStreamException("The app disconnected from the test host.");

        return JsonSerializer.Deserialize<TestHostRequestMessage>(line, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize test-host request.");
    }

    private async Task WriteMessageAsync(TestHostResponseMessage message)
    {
        var serialized = JsonSerializer.Serialize(message, JsonOptions);
        await _writer!.WriteLineAsync(serialized);
    }

    private static void EnsureResponseMatchesRequest(TestHostRequestMessage request, TestHostResponseMessage response)
    {
        var expectedResponseType = request.Type switch
        {
            "request-line" => "line",
            "request-key" => "key",
            _ => throw new InvalidOperationException($"Unexpected request type \"{request.Type}\".")
        };

        if (!string.Equals(response.Type, expectedResponseType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected a \"{expectedResponseType}\" response for request \"{request.Type}\" but queued \"{response.Type}\".");
        }
    }

    private void ThrowIfFaulted()
    {
        if (_backgroundFailure != null)
            throw new InvalidOperationException("The test host client failed while processing requests.", _backgroundFailure);
    }

    private sealed class QueuedResponse
    {
        public QueuedResponse(TestHostResponseMessage message)
        {
            Message = message;
        }

        public TestHostResponseMessage Message { get; }

        public TaskCompletionSource Consumed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
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
