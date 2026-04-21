using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Tests;

public class MapConflictResolverTests
{
    private const string OursTimestamp = "2026-04-20T10:00:00Z";
    private const string TheirsTimestamp = "2026-04-20T09:00:00Z";
    private const string NewerTheirsTimestamp = "2026-04-20T11:00:00Z";

    private static string BuildConflict(string oursTimestamp, string theirsTimestamp, string sha = "abc123") =>
        $$"""
        {
          "updatedAt": "{{oursTimestamp}}"
        <<<<<<< HEAD
          ,"rootNode": {}
        =======
          ,"rootNode": {}
        >>>>>>> {{sha}}
        }
        """;

    private static string BuildUpdatedAtConflict(string oursTimestamp, string theirsTimestamp, string sha = "abc123") =>
        "{\n" +
        $"<<<<<<< HEAD\n" +
        $"  \"updatedAt\": \"{oursTimestamp}\"\n" +
        "=======\n" +
        $"  \"updatedAt\": \"{theirsTimestamp}\"\n" +
        $">>>>>>> {sha}\n" +
        "}";

    private static string BuildUpdatedAtConflictWithTrailingBrace(string oursTimestamp, string theirsTimestamp, string sha = "abc123") =>
        "{\n" +
        "  \"rootNode\": {},\n" +
        $"<<<<<<< HEAD\n" +
        $"  \"updatedAt\": \"{oursTimestamp}\"\n" +
        "}\n" +
        "=======\n" +
        $"  \"updatedAt\": \"{theirsTimestamp}\"\n" +
        "}\n" +
        $">>>>>>> {sha}";

    [Fact]
    public void BuildResolvedContent_TakeOurs_ReturnsOursSide()
    {
        var conflict = BuildUpdatedAtConflict(OursTimestamp, TheirsTimestamp);
        var result = MapConflictResolver.BuildResolvedContent(conflict, takeOurs: true);
        Assert.Contains(OursTimestamp, result);
        Assert.DoesNotContain(TheirsTimestamp, result);
        Assert.DoesNotContain("<<<<<<<", result);
        Assert.DoesNotContain("=======", result);
        Assert.DoesNotContain(">>>>>>>", result);
    }

    [Fact]
    public void BuildResolvedContent_TakeTheirs_ReturnTheirsSide()
    {
        var conflict = BuildUpdatedAtConflict(OursTimestamp, TheirsTimestamp);
        var result = MapConflictResolver.BuildResolvedContent(conflict, takeOurs: false);
        Assert.Contains(TheirsTimestamp, result);
        Assert.DoesNotContain(OursTimestamp, result);
        Assert.DoesNotContain("<<<<<<<", result);
        Assert.DoesNotContain("=======", result);
        Assert.DoesNotContain(">>>>>>>", result);
    }

    [Fact]
    public void TryResolve_WhenOursIsNewer_TakesOurs()
    {
        var conflict = BuildUpdatedAtConflict(OursTimestamp, TheirsTimestamp);
        var resolved = MapConflictResolver.TryResolve(conflict, out var content);
        Assert.True(resolved);
        Assert.Contains(OursTimestamp, content);
        Assert.DoesNotContain(TheirsTimestamp, content);
    }

    [Fact]
    public void TryResolve_WhenTheirsIsNewer_TakesTheirs()
    {
        var conflict = BuildUpdatedAtConflict(OursTimestamp, NewerTheirsTimestamp);
        var resolved = MapConflictResolver.TryResolve(conflict, out var content);
        Assert.True(resolved);
        Assert.Contains(NewerTheirsTimestamp, content);
        Assert.DoesNotContain(OursTimestamp, content);
    }

    [Fact]
    public void TryResolve_WhenTimestampsEqual_TakesTheirs()
    {
        var conflict = BuildUpdatedAtConflict(OursTimestamp, OursTimestamp);
        var resolved = MapConflictResolver.TryResolve(conflict, out var content);
        Assert.True(resolved);
        Assert.NotNull(content);
    }

    [Fact]
    public void TryResolve_HandlesTrailingBraceInConflictRegion()
    {
        var conflict = BuildUpdatedAtConflictWithTrailingBrace(OursTimestamp, TheirsTimestamp);
        var resolved = MapConflictResolver.TryResolve(conflict, out var content);
        Assert.True(resolved);
        Assert.Contains(OursTimestamp, content);
        Assert.DoesNotContain("<<<<<<<", content);
    }

    [Fact]
    public void TryResolve_WhenNoConflictMarkers_ReturnsFalse()
    {
        var noConflict = "{\"updatedAt\": \"2026-04-20T10:00:00Z\"}";
        var resolved = MapConflictResolver.TryResolve(noConflict, out var content);
        Assert.False(resolved);
        Assert.Null(content);
    }

    [Fact]
    public void TryResolve_WhenInvalidJson_ReturnsFalse()
    {
        var invalid =
            "<<<<<<< HEAD\nnot json\n=======\nalso not json\n>>>>>>> abc";
        var resolved = MapConflictResolver.TryResolve(invalid, out var content);
        Assert.False(resolved);
        Assert.Null(content);
    }

    [Fact]
    public void TryResolve_NormalizesCrlfLineEndings()
    {
        var conflict = BuildUpdatedAtConflict(OursTimestamp, TheirsTimestamp).Replace("\n", "\r\n");
        var resolved = MapConflictResolver.TryResolve(conflict, out var content);
        Assert.True(resolved);
        Assert.NotNull(content);
    }

    [Fact]
    public void TryResolve_RealWorldConflict_TwoInlineBlocks_OursNewer_ResolvesToOurs()
    {
        // Exact conflict captured from a real git merge. Two inline conflict blocks:
        // one inside metadata.updatedAtUtc, one around the closing updatedAt + }.
        const string conflict = """
            {
              "rootNode": {
                "nodeType": 0,
                "uniqueIdentifier": "53ba90f9-f653-4771-bc08-3c8a531b9b85",
                "name": "საყოფაცხოვრებო",
                "children": [],
                "links": {},
                "number": 1,
                "collapsed": false,
                "hideDoneTasks": true,
                "taskState": 0,
                "metadata": {
                  "createdAtUtc": "2026-03-31T10:24:00Z",
            <<<<<<< HEAD
                  "updatedAtUtc": "2026-04-20T08:26:50Z",
            =======
                  "updatedAtUtc": "2026-04-20T08:26:19Z",
            >>>>>>> b218c10b5c35aeeba59b7f99a4b51225a26c1b48
                  "source": "manual",
                  "device": "focus-pwa-web",
                  "attachments": []
                }
              },
            <<<<<<< HEAD
              "updatedAt": "2026-04-20T08:26:50Z"
            }
            =======
              "updatedAt": "2026-04-20T08:26:19Z"
            }
            >>>>>>> b218c10b5c35aeeba59b7f99a4b51225a26c1b48
            """;

        var resolved = MapConflictResolver.TryResolve(conflict, out var content);

        Assert.True(resolved);
        // HEAD (ours) is newer on both fields — the merged result keeps the later timestamps
        Assert.Contains("2026-04-20T08:26:50Z", content);
        Assert.DoesNotContain("2026-04-20T08:26:19Z", content);
        Assert.DoesNotContain("<<<<<<<", content);
        Assert.DoesNotContain("=======", content);
        Assert.DoesNotContain(">>>>>>>", content);
        // Non-conflicting fields are preserved
        Assert.Contains("53ba90f9-f653-4771-bc08-3c8a531b9b85", content);
        Assert.Contains("საყოფაცხოვრებო", content);
        Assert.Contains("hideDoneTasks", content);
    }

    [Fact]
    public void BuildResolvedContent_MultipleConflictBlocks_ResolvesAll()
    {
        var conflict =
            "{\n" +
            "<<<<<<< HEAD\n" +
            "  \"a\": \"ours-a\",\n" +
            "=======\n" +
            "  \"a\": \"theirs-a\",\n" +
            ">>>>>>> sha\n" +
            "  \"stable\": true,\n" +
            "<<<<<<< HEAD\n" +
            $"  \"updatedAt\": \"{OursTimestamp}\"\n" +
            "=======\n" +
            $"  \"updatedAt\": \"{TheirsTimestamp}\"\n" +
            ">>>>>>> sha\n" +
            "}";

        var ours = MapConflictResolver.BuildResolvedContent(conflict, takeOurs: true);
        var theirs = MapConflictResolver.BuildResolvedContent(conflict, takeOurs: false);

        Assert.Contains("ours-a", ours);
        Assert.Contains(OursTimestamp, ours);
        Assert.DoesNotContain("theirs-a", ours);

        Assert.Contains("theirs-a", theirs);
        Assert.Contains(TheirsTimestamp, theirs);
        Assert.DoesNotContain("ours-a", theirs);
    }
}
