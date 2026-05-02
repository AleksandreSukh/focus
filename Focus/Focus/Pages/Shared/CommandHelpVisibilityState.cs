#nullable enable

using System;

namespace Systems.Sanity.Focus.Pages.Shared;

internal sealed class CommandHelpVisibilityState
{
    public const char ToggleKeyChar = '~';

    public bool ShowCommands { get; private set; }

    public bool TryHandlePreviewKey(ConsoleKeyInfo keyInfo, string currentText)
    {
        if (!string.IsNullOrEmpty(currentText) || keyInfo.KeyChar != ToggleKeyChar)
            return false;

        ShowCommands = !ShowCommands;
        return true;
    }

    public static ConsoleKeyInfo BuildToggleKeyInfo() =>
        new(ToggleKeyChar, ConsoleKey.Oem3, shift: true, alt: false, control: false);
}
