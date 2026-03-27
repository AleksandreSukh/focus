using System;
using System.Linq;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Tests;

public class SearchResultDisplayFormatterTests
{
    [Fact]
    public void Format_SearchResults_PreservesPlainTextAndColorsPathMatches()
    {
        var result = CreateResult(
            mapName: "work",
            taskState: TaskState.Todo,
            contextLabel: null,
            "Root",
            "Alpha",
            "Ship docs");

        var formatted = SearchResultDisplayFormatter.Format(
            result,
            new SearchResultDisplayOptions(
                includeMapName: true,
                colorizeAncestorPath: true,
                highlightTerms: new[] { "alp", "ship" }));

        Assert.Equal(result.ToDisplayString(includeMapName: true), PlainTextInlineFormatter.ToPlainText(formatted));

        var runs = InlineFormatParser.Parse(formatted);
        Assert.Contains(runs, run => run.Text.Contains("work: [ ] ") && run.ForegroundColor == null);
        Assert.Contains(runs, run => run.Text == "Root" && run.ForegroundColor == ConsoleColor.Yellow);
        Assert.Equal(2, runs.Count(run => run.Text == " > " && run.ForegroundColor == ConsoleColor.DarkYellow));
        Assert.Contains(runs, run => run.Text == "Alp" && run.ForegroundColor == ConsoleColor.Cyan);
        Assert.Contains(runs, run => run.Text == "ha" && run.ForegroundColor == ConsoleColor.Yellow);
        Assert.Contains(runs, run => run.Text == "Ship" && run.ForegroundColor == ConsoleColor.Cyan);
        Assert.Contains(runs, run => run.Text == " docs" && run.ForegroundColor == null);
    }

    [Fact]
    public void Format_TaskResults_ColorAncestorsWithoutHighlights()
    {
        var result = CreateResult(
            mapName: "work",
            taskState: TaskState.Doing,
            contextLabel: null,
            "Root",
            "Project",
            "Ship docs");

        var formatted = SearchResultDisplayFormatter.Format(
            result,
            new SearchResultDisplayOptions(
                includeMapName: false,
                colorizeAncestorPath: true,
                highlightTerms: Array.Empty<string>()));

        Assert.Equal(result.ToDisplayString(includeMapName: false), PlainTextInlineFormatter.ToPlainText(formatted));

        var runs = InlineFormatParser.Parse(formatted);
        Assert.DoesNotContain(runs, run => run.ForegroundColor == ConsoleColor.Cyan);
        Assert.Contains(runs, run => run.Text == "Root" && run.ForegroundColor == ConsoleColor.Yellow);
        Assert.Contains(runs, run => run.Text == "Project" && run.ForegroundColor == ConsoleColor.Yellow);
        Assert.Equal(2, runs.Count(run => run.Text == " > " && run.ForegroundColor == ConsoleColor.DarkYellow));
        Assert.Contains(runs, run => run.Text.Contains("Ship docs") && run.ForegroundColor == null);
    }

    [Fact]
    public void Format_LinkResults_StayPlainWhenPathColoringDisabled()
    {
        var result = CreateResult(
            mapName: "alpha",
            taskState: TaskState.Done,
            contextLabel: "backlink: related",
            "Root",
            "Linked node");

        var formatted = SearchResultDisplayFormatter.Format(
            result,
            new SearchResultDisplayOptions(
                includeMapName: true,
                colorizeAncestorPath: false,
                highlightTerms: Array.Empty<string>()));

        Assert.Equal(result.ToDisplayString(includeMapName: true), formatted);
        Assert.All(InlineFormatParser.Parse(formatted), run => Assert.Null(run.ForegroundColor));
    }

    [Fact]
    public void Format_OverlappingTerms_PrefersLongerMatch()
    {
        var result = CreateResult(
            mapName: "work",
            taskState: TaskState.None,
            contextLabel: null,
            "Alphabet");

        var formatted = SearchResultDisplayFormatter.Format(
            result,
            new SearchResultDisplayOptions(
                includeMapName: false,
                colorizeAncestorPath: true,
                highlightTerms: new[] { "alpha", "alphabet" }));

        var runs = InlineFormatParser.Parse(formatted);
        Assert.Contains(runs, run => run.Text == "Alphabet" && run.ForegroundColor == ConsoleColor.Cyan);
        Assert.DoesNotContain(runs, run => run.Text == "alpha" && run.ForegroundColor == ConsoleColor.Cyan);
    }

    private static NodeSearchResult CreateResult(
        string mapName,
        TaskState taskState,
        string? contextLabel,
        params string[] nodePathSegments)
    {
        return new NodeSearchResult(
            Guid.NewGuid(),
            nodePathSegments[^1],
            string.Join(" > ", nodePathSegments),
            @"C:\maps\sample.md",
            mapName,
            Score: 0,
            Depth: Math.Max(0, nodePathSegments.Length - 1),
            ContextLabel: contextLabel,
            TaskState: taskState)
        {
            NodePathSegments = nodePathSegments
        };
    }
}
