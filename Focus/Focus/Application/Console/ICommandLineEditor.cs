#nullable enable

using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;

namespace Systems.Sanity.Focus.Application.Console;

public interface ICommandLineEditor
{
    bool HistoryEnabled { get; set; }

    void SetAutoCompletionHandler(IAutoCompleteHandler handler);

    string Read(
        string prompt,
        string defaultInput = "",
        Action<string>? beforeEachAutoCompleteSuggestionWrite = null,
        Action<string>? afterEachAutoCompleteSuggestionWrite = null);

    IReadOnlyList<string> GetHistory();
}
