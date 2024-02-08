using System;
using System.Text;

namespace Systems.Sanity.Focus.Pages.Shared.Dialogs;

public static class DialogExtensions
{
    public static void AppendLineCentered(this StringBuilder stringBuilder, string input)
    {
        var messageActualLength = input.Length;
        var spacesNeededToCenterTheMessage = (Console.WindowWidth / 2) - (messageActualLength / 2);

        if (spacesNeededToCenterTheMessage > 0)
        {
            stringBuilder.Append(new string(' ', spacesNeededToCenterTheMessage));
        }

        stringBuilder.AppendLine(input);
    }
}