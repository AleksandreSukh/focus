using Systems.Sanity.Focus.Infrastructure.Input.ReadLine.Abstractions;

namespace Systems.Sanity.Focus.Infrastructure.Input.ReadLine
{
    public static class ReadLine
    {
        private static readonly object _renderLock = new();
        private static List<string> _history;
        private static readonly Queue<string> _pendingBackgroundMessages = new();
        private static bool _isReading;

        static ReadLine()
        {
            _history = new List<string>();
        }

        public static void AddHistory(params string[] text) => _history.AddRange(text);
        public static List<string> GetHistory() => _history;
        public static void ClearHistory() => _history = new List<string>();
        public static bool HistoryEnabled { get; set; }
        public static IAutoCompleteHandler AutoCompletionHandler { private get; set; }

        public static string Read(
            string prompt = "",
            string @default = "",
            Action<string>? beforeEachSuggestionWordWrite = null,
            Action<string>? afterEachSuggestionWordWrite = null,
            Func<ConsoleKeyInfo, string, bool>? previewKeyHandler = null,
            ConsoleKeyInfo? initialKeyInfo = null)
        {
            var console = new Console2();
            KeyHandler keyHandler = new KeyHandler(
                console,
                _history,
                AutoCompletionHandler,
                beforeEachSuggestionWordWrite,
                afterEachSuggestionWordWrite,
                previewKeyHandler);
            BeginRead(console, prompt);

            string text = GetText(keyHandler, initialKeyInfo);

            if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(@default))
            {
                text = @default;
            }
            else
            {
                if (HistoryEnabled)
                    _history.Add(text);
            }

            return text;
        }

        public static string ReadPassword(string prompt = "")
        {
            var console = new Console2 { PasswordMode = true };
            BeginRead(console, prompt);
            KeyHandler keyHandler = new KeyHandler(console, null, null);
            return GetText(keyHandler);
        }

        public static void WriteInterleavedMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            lock (_renderLock)
            {
                if (_isReading)
                {
                    _pendingBackgroundMessages.Enqueue(text);
                    return;
                }

                var console = new Console2();
                FlushPendingBackgroundMessages(console);
                WriteBackgroundMessage(console, text);
            }
        }

        private static string GetText(KeyHandler keyHandler, ConsoleKeyInfo? initialKeyInfo = null)
        {
            if (initialKeyInfo.HasValue && initialKeyInfo.Value.Key != ConsoleKey.Enter)
            {
                lock (_renderLock)
                {
                    keyHandler.Handle(initialKeyInfo.Value);
                }
            }

            ConsoleKeyInfo keyInfo = Console.ReadKey(true);
            while (keyInfo.Key != ConsoleKey.Enter)
            {
                lock (_renderLock)
                {
                    keyHandler.Handle(keyInfo);
                }

                keyInfo = Console.ReadKey(true);
            }

            lock (_renderLock)
            {
                Console.WriteLine();
                _isReading = false;
                return keyHandler.Text;
            }
        }

        private static void BeginRead(IConsole console, string prompt)
        {
            lock (_renderLock)
            {
                FlushPendingBackgroundMessages(console);
                _isReading = true;
                console.Write(prompt);
            }
        }

        private static void FlushPendingBackgroundMessages(IConsole console)
        {
            while (_pendingBackgroundMessages.Count > 0)
            {
                WriteBackgroundMessage(console, _pendingBackgroundMessages.Dequeue());
            }
        }

        private static void WriteBackgroundMessage(IConsole console, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            console.Write(text.TrimEnd('\r', '\n'));
            console.WriteLine(string.Empty);
        }
    }
}
