#nullable enable

using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;

namespace Systems.Sanity.Focus.Application.Console;

internal sealed class ReadLineCommandLineEditor : ICommandLineEditor
{
    public bool HistoryEnabled
    {
        get => ReadLine.HistoryEnabled;
        set => ReadLine.HistoryEnabled = value;
    }

    public IReadOnlyList<string> GetHistory() => ReadLine.GetHistory();

    public string Read(
        string prompt,
        string defaultInput = "",
        Action<string>? beforeEachAutoCompleteSuggestionWrite = null,
        Action<string>? afterEachAutoCompleteSuggestionWrite = null,
        Func<ConsoleKeyInfo, string, bool>? previewKeyHandler = null,
        ConsoleKeyInfo? initialKeyInfo = null)
    {
        return ReadLine.Read(
            prompt,
            defaultInput,
            beforeEachAutoCompleteSuggestionWrite,
            afterEachAutoCompleteSuggestionWrite,
            previewKeyHandler,
            initialKeyInfo);
    }

    public string ReadMultiline(string prompt, string defaultInput = "")
    {
        return MultilineInputCollector.Read(
            linePrompt => ReadLine.Read(linePrompt),
            prompt,
            defaultInput);
    }

    public void WriteInterleavedMessage(string text)
    {
        ReadLine.WriteInterleavedMessage(text);
    }

    public void SetAutoCompletionHandler(IAutoCompleteHandler handler)
    {
        ReadLine.AutoCompletionHandler = handler;
    }
}
