using System.Collections.Generic;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal abstract class BlockTextEditPage : Page
{
    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index) =>
        System.Array.Empty<string>();

    protected string GetMultilineInput(string prompt, string defaultInput = "")
    {
        var editor = AppConsole.Current.CommandLineEditor;
        var previousHistoryEnabled = editor.HistoryEnabled;
        editor.HistoryEnabled = false;

        try
        {
            return editor.ReadMultiline(prompt, defaultInput);
        }
        finally
        {
            editor.HistoryEnabled = previousHistoryEnabled;
        }
    }
}
