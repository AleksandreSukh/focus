using Systems.Sanity.Focus;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Tests;

public class CommandHelpFormatterTests
{
    [Fact]
    public void BuildGroupedLines_PreservesOrderAndOmitsEmptyGroups()
    {
        var output = CommandHelpFormatter.BuildGroupedLines(
            new[]
            {
                new CommandHelpGroup("Empty", Array.Empty<string>()),
                new CommandHelpGroup("Navigate", new[] { "cd <child>", "up" }),
                new CommandHelpGroup("To Do", new[] { "todo/td [child]" })
            },
            maxWidth: 120);

        Assert.DoesNotContain("Empty: ", output);
        Assert.Contains(ColorLabel("Navigate"), output);
        Assert.Contains(ColorLabel("To Do"), output);
        Assert.DoesNotContain(ColorEntry("cd <child>"), output);
        Assert.True(output.IndexOf(ColorLabel("Navigate"), StringComparison.InvariantCulture) <
                    output.IndexOf(ColorLabel("To Do"), StringComparison.InvariantCulture));
    }

    [Fact]
    public void BuildGroupedLines_WrapsAndAlignsContinuationLines()
    {
        var output = CommandHelpFormatter.BuildGroupedLines(
            new[]
            {
                new CommandHelpGroup("Go to", new[] { "a/1", "b/2", "c/3" })
            },
            maxWidth: 14);

        var lines = output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.True(lines.Length >= 2);
        Assert.StartsWith(ColorLabel("Go to"), lines[0]);
        Assert.StartsWith(new string(' ', "Go to: ".Length), lines[1]);
    }

    [Fact]
    public void BuildWrappedOptionList_WrapsAcrossMultipleLines()
    {
        var output = CommandHelpFormatter.BuildWrappedOptionList(
            "Valid options are",
            new[] { "rename", "delete", "search" },
            maxWidth: 24);

        var lines = output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.True(lines.Length >= 2);
        Assert.StartsWith(ColorLabel("Valid options are"), lines[0]);
        Assert.DoesNotContain(ColorEntry("rename"), output);
        Assert.StartsWith(new string(' ', "Valid options are: ".Length), lines[1]);
    }

    [Fact]
    public void BuildInputErrorMessageDialogText_UsesWrappedOptionsLayout()
    {
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession(24));

        var output = PageWithExclusiveOptions.BuildInputErrorMessageDialogText(new[] { "rename", "delete", "search" });

        Assert.Contains("*** Wrong Input ***", output);
        Assert.Contains(ColorLabel("Valid options are"), output);
        Assert.Contains(Environment.NewLine + new string(' ', "Valid options are: ".Length), output);
    }

    private static string ColorLabel(string label) =>
        $"[{ConfigurationConstants.CommandColor}]{label}[!]: ";

    private static string ColorEntry(string entry) =>
        $"[{ConfigurationConstants.CommandColor}]{entry}[!]";
}
