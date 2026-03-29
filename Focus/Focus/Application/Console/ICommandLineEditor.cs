#nullable enable

using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;

namespace Systems.Sanity.Focus.Application.Console;

public interface ICommandLineEditor
{
    bool HistoryEnabled { get; set; }

    void SetAutoCompletionHandler(IAutoCompleteHandler handler);

    void WriteInterleavedMessage(string text);

    string Read(
        string prompt,
        string defaultInput = "",
        Action<string>? beforeEachAutoCompleteSuggestionWrite = null,
        Action<string>? afterEachAutoCompleteSuggestionWrite = null,
        Func<ConsoleKeyInfo, string, bool>? previewKeyHandler = null,
        ConsoleKeyInfo? initialKeyInfo = null);

    IReadOnlyList<string> GetHistory();
}
