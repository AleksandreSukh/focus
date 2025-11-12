using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal abstract class TextEditPage : Page
{
    public override void BeforeEachAutoComplete(string str)
    {
        if (ColorfulConsole.ColorTagsToConsoleColorDict.TryGetValue(str, out ConsoleColor color))
        {
            if (Console.BackgroundColor != color)
            {
                Console.ForegroundColor = color;
            }
        }
    }

    public override void AfterEachAutoComplete(string str)
    {
        Console.ForegroundColor = ConsoleWrapper.DefaultColor;
    }

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
    {
        var currentText = text.Substring(index);

        if (!text.EndsWith(ColorfulConsole.CommandEndBracket))
            foreach (var cc in ReadLine.GetHistory().Where(hc => ColorfulConsole.ColorCommands.Contains(hc)).Union(ColorfulConsole.ColorCommands))
            {
                yield return currentText + cc;
            }

        yield return currentText;
    }
}