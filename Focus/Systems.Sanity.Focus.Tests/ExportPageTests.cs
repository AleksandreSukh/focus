using System.Reflection;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Edit;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;

namespace Systems.Sanity.Focus.Tests;

public class ExportPageTests
{
    [Fact]
    public void Suggestions_DoNotIncludeBackgroundToggle_WhenFormatIsMarkdown()
    {
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession());
        var page = new ExportPage("alpha");

        var suggestions = page.GetSuggestions(string.Empty, 0);
        var screen = BuildScreen(page);

        Assert.Contains("attachments", suggestions);
        Assert.DoesNotContain("noattachments", suggestions);
        Assert.DoesNotContain("blackbg", suggestions);
        Assert.DoesNotContain("lightbg", suggestions);
        Assert.Contains("Attachments: Excluded", screen);
        Assert.DoesNotContain("Background:", screen);
        Assert.DoesNotContain("blackbg", screen);
        Assert.DoesNotContain("lightbg", screen);
    }

    [Fact]
    public void HtmlFormat_MakesBlackBackgroundToggleVisible()
    {
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession());
        var page = new ExportPage("alpha");

        SendInput(page, "html");

        var suggestions = page.GetSuggestions(string.Empty, 0);
        var screen = BuildScreen(page);

        Assert.Contains("blackbg", suggestions);
        Assert.DoesNotContain("lightbg", suggestions);
        Assert.Contains("Background: Light", screen);
        Assert.Contains("blackbg", screen);
        Assert.DoesNotContain("lightbg", screen);
    }

    [Fact]
    public void BlackBackgroundToggle_ReplacesVisibleOption()
    {
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession());
        var page = new ExportPage("alpha");

        SendInput(page, "html");
        SendInput(page, "blackbg");

        var suggestions = page.GetSuggestions(string.Empty, 0);
        var screen = BuildScreen(page);

        Assert.DoesNotContain("blackbg", suggestions);
        Assert.Contains("lightbg", suggestions);
        Assert.Contains("Background: Black", screen);
        Assert.DoesNotContain("blackbg", screen);
        Assert.Contains("lightbg", screen);
    }

    [Fact]
    public void AttachmentsToggle_ReplacesVisibleOptionAndUpdatesScreen()
    {
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession());
        var page = new ExportPage("alpha");

        SendInput(page, "attachments");

        var suggestions = page.GetSuggestions(string.Empty, 0);
        var screen = BuildScreen(page);

        Assert.DoesNotContain("attachments", suggestions);
        Assert.Contains("noattachments", suggestions);
        Assert.Contains("Attachments: Included", screen);
        Assert.DoesNotContain("\"attachments\" - include attachments in export", screen);
        Assert.Contains("noattachments", screen);
    }

    [Fact]
    public void CopyTextOption_IsVisibleSuggestedAndSelectsClipboardDestination()
    {
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession());
        var page = new ExportPage("alpha");

        var suggestions = page.GetSuggestions(string.Empty, 0);
        var screen = BuildScreen(page);

        Assert.Contains("copytext", suggestions);
        Assert.Contains("copytext", screen);

        SendInput(page, "collapsed");
        SendInput(page, "copytext");

        Assert.NotNull(page.SelectedExport);
        Assert.Equal(ExportDestination.ClipboardText, page.SelectedExport!.Destination);
        Assert.Equal(ExportFormat.PlainText, page.SelectedExport.Format);
        Assert.True(page.SelectedExport.SkipCollapsedDescendants);
        Assert.False(page.SelectedExport.IncludeAttachments);
    }

    [Fact]
    public void Show_SaveFromHtmlWithBlackBackground_PreservesFlagInRequest()
    {
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("html", "blackbg", "save"));
        var page = new ExportPage("alpha");

        page.Show();

        Assert.NotNull(page.SelectedExport);
        Assert.Equal(ExportFormat.Html, page.SelectedExport!.Format);
        Assert.True(page.SelectedExport.UseBlackBackground);
    }

    [Fact]
    public void Show_SaveFromMarkdown_LeavesBlackBackgroundDisabled()
    {
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("save"));
        var page = new ExportPage("alpha");

        page.Show();

        Assert.NotNull(page.SelectedExport);
        Assert.Equal(ExportFormat.Markdown, page.SelectedExport!.Format);
        Assert.False(page.SelectedExport.UseBlackBackground);
    }

    [Fact]
    public void Show_SaveWithAttachments_PreservesFlagInRequest()
    {
        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession("attachments", "save"));
        var page = new ExportPage("alpha");

        page.Show();

        Assert.NotNull(page.SelectedExport);
        Assert.True(page.SelectedExport!.IncludeAttachments);
    }

    private static void SendInput(ExportPage page, string input)
    {
        typeof(ExportPage)
            .GetMethod("HandleInput", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, new object[] { new ConsoleInput(input) });
    }

    private static string BuildScreen(ExportPage page)
    {
        return (string)typeof(ExportPage)
            .GetMethod("BuildScreen", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, null)!;
    }
}
