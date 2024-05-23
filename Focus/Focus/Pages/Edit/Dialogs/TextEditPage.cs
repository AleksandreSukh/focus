using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal abstract class TextEditPage : Page
{
    private static readonly HashSet<string> ColorCommands = new[] { ColorfulConsole.ColorCommandTerminationTag }
        .Union(ColorfulConsole.PrimaryColors)
        .Union(ColorfulConsole.Colors)
        .Select(c => $"{ColorfulConsole.CommandStartBracket}{c}{ColorfulConsole.CommandEndBracket}")
        .ToHashSet();

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
    {
        var currentText = text.Substring(index);

        if (!text.EndsWith(ColorfulConsole.CommandEndBracket))
            foreach (var cc in ReadLine.GetHistory().Where(hc => ColorCommands.Contains(hc)).Union(ColorCommands))
            {
                yield return currentText + cc;
            }

        yield return currentText;
    }
}