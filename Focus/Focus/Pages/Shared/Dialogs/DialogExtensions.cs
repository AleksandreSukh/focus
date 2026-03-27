using System;
using System.Text;
using Systems.Sanity.Focus.Application;

namespace Systems.Sanity.Focus.Pages.Shared.Dialogs;

public static class DialogExtensions
{
    public static void AppendLineCentered(this StringBuilder stringBuilder, string input)
    {
        var messageActualLength = input.Length;
        var spacesNeededToCenterTheMessage = (AppConsole.Current.WindowWidth / 2) - (messageActualLength / 2);

        if (spacesNeededToCenterTheMessage > 0)
        {
            stringBuilder.Append(new string(' ', spacesNeededToCenterTheMessage));
        }

        stringBuilder.AppendLine(input);
    }
}
