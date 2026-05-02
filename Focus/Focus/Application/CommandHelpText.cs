#nullable enable

using System;

namespace Systems.Sanity.Focus.Application;

internal static class CommandHelpText
{
    public const string HiddenHelpMessage = "Commands hidden. Press \"~\" to show.";

    public static string BuildHiddenHelpLine() =>
        $":i {HiddenHelpMessage}{Environment.NewLine}";
}
