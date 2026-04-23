using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Tests;

/// <summary>
/// Tests for Phase A (field-level merge) in MapConflictResolver.
/// Each test builds two clean JSON documents (ours / theirs) representing what
/// each side of a merge conflict looks like after stripping conflict markers,
/// then asserts that TryMergeResolve produces the correct merged output.
/// </summary>
public class MapConflictResolverMergeTests
{
    // -------------------------------------------------------------------------
    // Test document builders
    // -------------------------------------------------------------------------

    private static readonly string RootId = "00000000-0000-0000-0000-000000000001";
    private static readonly string ChildId = "00000000-0000-0000-0000-000000000002";
    private static readonly string OtherChildId = "00000000-0000-0000-0000-000000000003";

    // Build JObjects directly — never via string parsing — so timestamp strings
    // remain as raw string tokens rather than being auto-converted to DateTimeOffset.
    private static JObject BaseMap(
        string updatedAt = "2026-04-20T08:00:00Z",
        string rootUpdatedAtUtc = "2026-04-20T08:00:00Z",
        string rootCreatedAtUtc = "2026-04-20T07:00:00Z",
        string rootName = "Root",
        string source = "manual",
        string device = "pc")
    {
        return new JObject
        {
            ["rootNode"] = new JObject
            {
                ["nodeType"] = 0,
                ["uniqueIdentifier"] = RootId,
                ["name"] = rootName,
                ["children"] = new JArray(),
                ["links"] = new JObject(),
                ["number"] = 1,
                ["collapsed"] = false,
                ["hideDoneTasks"] = false,
                ["taskState"] = 0,
                ["metadata"] = new JObject
                {
                    ["createdAtUtc"] = rootCreatedAtUtc,
                    ["updatedAtUtc"] = rootUpdatedAtUtc,
                    ["source"] = source,
                    ["device"] = device,
                    ["attachments"] = new JArray()
                }
            },
            ["updatedAt"] = updatedAt
        };
    }

    private static JObject AddChild(JObject map, string childId, string name,
        string updatedAtUtc = "2026-04-20T08:00:00Z",
        string createdAtUtc = "2026-04-20T07:00:00Z",
        int taskState = 0,
        bool collapsed = false)
    {
        var children = (JArray)map["rootNode"]!["children"]!;
        children.Add(new JObject
        {
            ["nodeType"] = 0,
            ["uniqueIdentifier"] = childId,
            ["name"] = name,
            ["children"] = new JArray(),
            ["links"] = new JObject(),
            ["number"] = children.Count + 1,
            ["collapsed"] = collapsed,
            ["hideDoneTasks"] = false,
            ["taskState"] = taskState,
            ["metadata"] = new JObject
            {
                ["createdAtUtc"] = createdAtUtc,
                ["updatedAtUtc"] = updatedAtUtc,
                ["source"] = "manual",
                ["device"] = "pc",
                ["attachments"] = new JArray()
            }
        });
        return map;
    }

    private static string Json(JObject obj) => obj.ToString(Formatting.Indented);

    // Parse result JSON with DateParseHandling.None so timestamp strings are
    // returned as raw strings, not re-formatted DateTimeOffset values.
    private static JToken Get(string json, string path)
    {
        using var reader = new JsonTextReader(new StringReader(json))
        {
            DateParseHandling = DateParseHandling.None
        };
        return JObject.Load(reader).SelectToken(path)!;
    }

    // -------------------------------------------------------------------------
    // 1. Map-level updatedAt (take max timestamp)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_OursNewerUpdatedAt_KeepsOursTimestamp()
    {
        var ours = BaseMap(updatedAt: "2026-04-20T10:00:00Z", rootUpdatedAtUtc: "2026-04-20T10:00:00Z");
        var theirs = BaseMap(updatedAt: "2026-04-20T09:00:00Z", rootUpdatedAtUtc: "2026-04-20T09:00:00Z");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("2026-04-20T10:00:00Z", Get(merged!, "updatedAt").Value<string>());
    }

    [Fact]
    public void TryMergeResolve_TheirsNewerUpdatedAt_TakesTheirsTimestamp()
    {
        var ours = BaseMap(updatedAt: "2026-04-20T09:00:00Z", rootUpdatedAtUtc: "2026-04-20T09:00:00Z");
        var theirs = BaseMap(updatedAt: "2026-04-20T11:00:00Z", rootUpdatedAtUtc: "2026-04-20T11:00:00Z");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("2026-04-20T11:00:00Z", Get(merged!, "updatedAt").Value<string>());
    }

    // -------------------------------------------------------------------------
    // 2. Node metadata.updatedAtUtc (take max)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_TheirsNewerNodeUpdatedAtUtc_TakesTheirsValue()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T10:00:00Z");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("2026-04-20T10:00:00Z", Get(merged!, "rootNode.metadata.updatedAtUtc").Value<string>());
    }

    [Fact]
    public void TryMergeResolve_OursNewerNodeUpdatedAtUtc_KeepsOursValue()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T12:00:00Z");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("2026-04-20T12:00:00Z", Get(merged!, "rootNode.metadata.updatedAtUtc").Value<string>());
    }

    // -------------------------------------------------------------------------
    // 3. metadata.createdAtUtc (take min — earlier is always correct)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_TheirsEarlierCreatedAtUtc_TakesTheirsEarlierValue()
    {
        var ours = BaseMap(rootCreatedAtUtc: "2026-04-20T08:00:00Z");
        var theirs = BaseMap(rootCreatedAtUtc: "2026-04-20T06:00:00Z");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("2026-04-20T06:00:00Z", Get(merged!, "rootNode.metadata.createdAtUtc").Value<string>());
    }

    [Fact]
    public void TryMergeResolve_OursEarlierCreatedAtUtc_KeepsOursEarlierValue()
    {
        var ours = BaseMap(rootCreatedAtUtc: "2026-04-20T05:00:00Z");
        var theirs = BaseMap(rootCreatedAtUtc: "2026-04-20T08:00:00Z");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("2026-04-20T05:00:00Z", Get(merged!, "rootNode.metadata.createdAtUtc").Value<string>());
    }

    // -------------------------------------------------------------------------
    // 4. collapsed flag (newer node wins)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_TheirsNodeNewerAndCollapsedTrue_TakesCollapsedTrue()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T10:00:00Z");
        theirs["rootNode"]!["collapsed"] = true;

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.True(Get(merged!, "rootNode.collapsed").Value<bool>());
    }

    [Fact]
    public void TryMergeResolve_OursNodeNewerAndCollapsedFalse_KeepsCollapsedFalse()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T12:00:00Z");
        ours["rootNode"]!["collapsed"] = false;
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z");
        theirs["rootNode"]!["collapsed"] = true;

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.False(Get(merged!, "rootNode.collapsed").Value<bool>());
    }

    // -------------------------------------------------------------------------
    // 5. hideDoneTasks flag (newer node wins)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_TheirsNodeNewerAndHideDoneTasksTrue_TakesTrue()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T10:00:00Z");
        theirs["rootNode"]!["hideDoneTasks"] = true;

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.True(Get(merged!, "rootNode.hideDoneTasks").Value<bool>());
    }

    [Fact]
    public void TryMergeResolve_OursNodeNewerAndHideDoneTasksFalse_KeepsFalse()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T12:00:00Z");
        ours["rootNode"]!["hideDoneTasks"] = false;
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z");
        theirs["rootNode"]!["hideDoneTasks"] = true;

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.False(Get(merged!, "rootNode.hideDoneTasks").Value<bool>());
    }

    [Fact]
    public void TryMergeResolve_TheirsNodeNewerAndHideDoneTasksExplicitTrue_TakesTrue()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T10:00:00Z");
        theirs["rootNode"]!["hideDoneTasks"] = false;
        theirs["rootNode"]!["hideDoneTasksExplicit"] = true;

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.True(Get(merged!, "rootNode.hideDoneTasksExplicit").Value<bool>());
    }

    // -------------------------------------------------------------------------
    // 6. taskState (newer node wins)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_TheirsNodeNewerWithHigherTaskState_TakesTheirsTaskState()
    {
        var ours = AddChild(BaseMap(), ChildId, "Task", updatedAtUtc: "2026-04-20T08:00:00Z", taskState: 1 /*Todo*/);
        var theirs = AddChild(BaseMap(), ChildId, "Task", updatedAtUtc: "2026-04-20T10:00:00Z", taskState: 2 /*Doing*/);

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal(2, Get(merged!, "rootNode.children[0].taskState").Value<int>());
    }

    [Fact]
    public void TryMergeResolve_OursNodeNewerWithDifferentTaskState_KeepsOursTaskState()
    {
        var ours = AddChild(BaseMap(), ChildId, "Task", updatedAtUtc: "2026-04-20T12:00:00Z", taskState: 3 /*Done*/);
        var theirs = AddChild(BaseMap(), ChildId, "Task", updatedAtUtc: "2026-04-20T08:00:00Z", taskState: 1 /*Todo*/);

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal(3, Get(merged!, "rootNode.children[0].taskState").Value<int>());
    }

    // -------------------------------------------------------------------------
    // 7. name (newer node wins)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_TheirsNodeNewerWithDifferentName_TakesTheirsName()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z", rootName: "Old name");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T10:00:00Z", rootName: "New name");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("New name", Get(merged!, "rootNode.name").Value<string>());
    }

    [Fact]
    public void TryMergeResolve_OursNodeNewerWithDifferentName_KeepsOursName()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T12:00:00Z", rootName: "Ours name");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z", rootName: "Theirs name");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("Ours name", Get(merged!, "rootNode.name").Value<string>());
    }

    // -------------------------------------------------------------------------
    // 8. links union
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_BothSidesHaveDifferentLinks_MergesAllLinks()
    {
        var linkIdA = "aaaaaaaa-0000-0000-0000-000000000001";
        var linkIdB = "bbbbbbbb-0000-0000-0000-000000000001";

        var ours = BaseMap();
        ((JObject)ours["rootNode"]!["links"]!)[linkIdA] = new JObject { ["id"] = linkIdA, ["relationType"] = 0 };

        var theirs = BaseMap();
        ((JObject)theirs["rootNode"]!["links"]!)[linkIdB] = new JObject { ["id"] = linkIdB, ["relationType"] = 0 };

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        var links = (JObject)Get(merged!, "rootNode.links");
        Assert.True(links.ContainsKey(linkIdA), "should contain ours link");
        Assert.True(links.ContainsKey(linkIdB), "should contain theirs link");
    }

    [Fact]
    public void TryMergeResolve_BothSidesHaveSameLinkGuid_KeepsOneCopy()
    {
        var linkId = "aaaaaaaa-0000-0000-0000-000000000001";
        var link = new JObject { ["id"] = linkId, ["relationType"] = 0 };

        var ours = BaseMap();
        ((JObject)ours["rootNode"]!["links"]!)[linkId] = link.DeepClone();
        var theirs = BaseMap();
        ((JObject)theirs["rootNode"]!["links"]!)[linkId] = link.DeepClone();

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        var links = (JObject)Get(merged!, "rootNode.links");
        Assert.Single(links);
    }

    // -------------------------------------------------------------------------
    // 9. attachments union
    // -------------------------------------------------------------------------

    private static JObject BuildAttachment(string id, string name) => new JObject
    {
        ["id"] = id,
        ["relativePath"] = name,
        ["mediaType"] = "image/png",
        ["displayName"] = name,
        ["createdAtUtc"] = "2026-04-20T08:00:00Z"
    };

    [Fact]
    public void TryMergeResolve_BothSidesHaveDifferentAttachments_MergesAllAttachments()
    {
        var idA = "aaaaaaaa-0000-0000-0000-000000000011";
        var idB = "bbbbbbbb-0000-0000-0000-000000000011";

        var ours = BaseMap();
        ((JArray)ours["rootNode"]!["metadata"]!["attachments"]!).Add(BuildAttachment(idA, "a.png"));
        var theirs = BaseMap();
        ((JArray)theirs["rootNode"]!["metadata"]!["attachments"]!).Add(BuildAttachment(idB, "b.png"));

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        var attachments = (JArray)Get(merged!, "rootNode.metadata.attachments");
        Assert.Equal(2, attachments.Count);
        Assert.Contains(attachments, t => t["id"]!.Value<string>() == idA);
        Assert.Contains(attachments, t => t["id"]!.Value<string>() == idB);
    }

    [Fact]
    public void TryMergeResolve_BothSidesHaveSameAttachmentId_KeepsOneCopy()
    {
        var id = "aaaaaaaa-0000-0000-0000-000000000011";

        var ours = BaseMap();
        ((JArray)ours["rootNode"]!["metadata"]!["attachments"]!).Add(BuildAttachment(id, "capture.png"));
        var theirs = BaseMap();
        ((JArray)theirs["rootNode"]!["metadata"]!["attachments"]!).Add(BuildAttachment(id, "capture.png"));

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        var attachments = (JArray)Get(merged!, "rootNode.metadata.attachments");
        Assert.Single(attachments);
    }

    // -------------------------------------------------------------------------
    // 10. metadata.source preference (never regress to legacy-import)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_OursSourceIsLegacy_TakesTheirsNonLegacySource()
    {
        // Ours has newer timestamp but legacy source — theirs non-legacy should win regardless
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T10:00:00Z", source: "legacy-import");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z", source: "manual");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("manual", Get(merged!, "rootNode.metadata.source").Value<string>());
    }

    [Fact]
    public void TryMergeResolve_TheirsSourceIsLegacy_KeepsOursNonLegacySource()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z", source: "clipboard-text");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T10:00:00Z", source: "legacy-import");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("clipboard-text", Get(merged!, "rootNode.metadata.source").Value<string>());
    }

    [Fact]
    public void TryMergeResolve_BothNonLegacySources_TheirsNewer_TakesTheirsSource()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z", source: "manual");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T10:00:00Z", source: "clipboard-text");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("clipboard-text", Get(merged!, "rootNode.metadata.source").Value<string>());
    }

    // -------------------------------------------------------------------------
    // 11. metadata.device (newer node wins)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_TheirsNewerNodeWithDifferentDevice_TakesTheirsDevice()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z", device: "pc");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T10:00:00Z", device: "ipad");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("ipad", Get(merged!, "rootNode.metadata.device").Value<string>());
    }

    [Fact]
    public void TryMergeResolve_OursNewerNodeWithDifferentDevice_KeepsOursDevice()
    {
        var ours = BaseMap(rootUpdatedAtUtc: "2026-04-20T12:00:00Z", device: "pc");
        var theirs = BaseMap(rootUpdatedAtUtc: "2026-04-20T08:00:00Z", device: "ipad");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("pc", Get(merged!, "rootNode.metadata.device").Value<string>());
    }

    // -------------------------------------------------------------------------
    // 12. Children: union of added children
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_BothAddedDifferentChildren_MergesAllChildren()
    {
        var ours = AddChild(BaseMap(), ChildId, "Ours child");
        var theirs = AddChild(BaseMap(), OtherChildId, "Theirs child");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        var children = (JArray)Get(merged!, "rootNode.children");
        Assert.Equal(2, children.Count);
        Assert.Contains(children, c => c["name"]!.Value<string>() == "Ours child");
        Assert.Contains(children, c => c["name"]!.Value<string>() == "Theirs child");
    }

    [Fact]
    public void TryMergeResolve_BothAddedDifferentChildren_RenumbersSequentially()
    {
        var ours = AddChild(BaseMap(), ChildId, "Ours child");
        var theirs = AddChild(BaseMap(), OtherChildId, "Theirs child");

        MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        var children = (JArray)Get(merged!, "rootNode.children");
        Assert.Equal(1, children[0]["number"]!.Value<int>());
        Assert.Equal(2, children[1]["number"]!.Value<int>());
    }

    [Fact]
    public void TryMergeResolve_SharedChildHasNameConflict_TakesNewerChildName()
    {
        var ours = AddChild(BaseMap(), ChildId, "Old name", updatedAtUtc: "2026-04-20T08:00:00Z");
        var theirs = AddChild(BaseMap(), ChildId, "New name", updatedAtUtc: "2026-04-20T10:00:00Z");

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("New name", Get(merged!, "rootNode.children[0].name").Value<string>());
    }

    // -------------------------------------------------------------------------
    // 13. Mixed independent edits on different nodes — the key value-add of Phase A
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_OursEditedOneNode_TheirsEditedAnotherNode_BothChangesPreserved()
    {
        // Ours: renamed child A (newer on A), child B untouched
        var ours = AddChild(
            AddChild(BaseMap(), ChildId, "Renamed by ours", updatedAtUtc: "2026-04-20T10:00:00Z"),
            OtherChildId, "Child B", updatedAtUtc: "2026-04-20T08:00:00Z");

        // Theirs: changed task state on child B (newer on B), child A untouched
        var theirs = AddChild(
            AddChild(BaseMap(), ChildId, "Original name A", updatedAtUtc: "2026-04-20T08:00:00Z"),
            OtherChildId, "Child B", updatedAtUtc: "2026-04-20T10:00:00Z", taskState: 1);

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        var children = (JArray)Get(merged!, "rootNode.children");
        var childA = children.First(c => c["uniqueIdentifier"]!.Value<string>() == ChildId);
        var childB = children.First(c => c["uniqueIdentifier"]!.Value<string>() == OtherChildId);

        Assert.Equal("Renamed by ours", childA["name"]!.Value<string>());
        Assert.Equal(1, childB["taskState"]!.Value<int>());
    }

    // -------------------------------------------------------------------------
    // 14. Phase A rejection → Phase B fallback
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_UnknownDifferingTopLevelField_ReturnsFalse()
    {
        var ours = BaseMap();
        ours["unknownField"] = "value-a";
        var theirs = BaseMap();
        theirs["unknownField"] = "value-b";

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.False(resolved);
        Assert.Null(merged);
    }

    [Fact]
    public void TryMergeResolve_UnknownDifferingNodeField_ReturnsFalse()
    {
        var ours = BaseMap();
        ours["rootNode"]!["unknownNodeField"] = "a";
        var theirs = BaseMap();
        theirs["rootNode"]!["unknownNodeField"] = "b";

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.False(resolved);
    }

    [Fact]
    public void TryMergeResolve_UnknownDifferingMetadataField_ReturnsFalse()
    {
        var ours = BaseMap();
        ours["rootNode"]!["metadata"]!["futureField"] = "x";
        var theirs = BaseMap();
        theirs["rootNode"]!["metadata"]!["futureField"] = "y";

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.False(resolved);
    }

    [Fact]
    public void TryResolve_WhenPhaseARejects_FallsBackToTimestampSelectionViaPhaseB()
    {
        // Phase A rejects because of an unknown differing field.
        // Phase B selects the whole document with the newer updatedAt.
        var oursDoc = BaseMap(updatedAt: "2026-04-20T10:00:00Z");
        oursDoc["rootNode"]!["unknownField"] = "ours-value";
        var theirsDoc = BaseMap(updatedAt: "2026-04-20T08:00:00Z");
        theirsDoc["rootNode"]!["unknownField"] = "theirs-value";

        var conflicted = $"<<<<<<< HEAD\n{Json(oursDoc)}\n=======\n{Json(theirsDoc)}\n>>>>>>> abc123";

        var resolved = MapConflictResolver.TryResolve(conflicted, out var content);

        Assert.True(resolved);
        Assert.Contains("ours-value", content);    // Phase B picked ours (newer updatedAt)
        Assert.DoesNotContain("theirs-value", content);
    }

    // -------------------------------------------------------------------------
    // 15. End-to-end TryResolve with conflict markers (Phase A path)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryResolve_WithUpdatedAtConflictMarkers_UsesPhaseAAndPicksNewerTimestamp()
    {
        const string oursTs = "2026-04-20T08:00:00Z";
        const string theirsTs = "2026-04-20T10:00:00Z";

        var conflicted =
            "{\n" +
            "<<<<<<< HEAD\n" +
            $"  \"updatedAt\": \"{oursTs}\"\n" +
            "=======\n" +
            $"  \"updatedAt\": \"{theirsTs}\"\n" +
            ">>>>>>> abc123\n" +
            "}";

        var resolved = MapConflictResolver.TryResolve(conflicted, out var content);

        Assert.True(resolved);
        Assert.Contains(theirsTs, content);
        Assert.DoesNotContain("<<<<<<<", content);
    }

    [Fact]
    public void TryResolve_WithWholeDocumentConflict_MergesViaPhaseA()
    {
        var oursDoc = AddChild(BaseMap(updatedAt: "2026-04-20T09:00:00Z"), ChildId, "Old name",
            updatedAtUtc: "2026-04-20T08:00:00Z", taskState: 1);
        var theirsDoc = AddChild(BaseMap(updatedAt: "2026-04-20T11:00:00Z"), ChildId, "New name",
            updatedAtUtc: "2026-04-20T10:00:00Z", taskState: 2);

        var conflicted = $"<<<<<<< HEAD\n{Json(oursDoc)}\n=======\n{Json(theirsDoc)}\n>>>>>>> abc123";

        var resolved = MapConflictResolver.TryResolve(conflicted, out var content);

        Assert.True(resolved);
        // Theirs child is newer → its name and taskState win
        Assert.Contains("New name", content);
        Assert.Contains("\"taskState\": 2", content);
        Assert.DoesNotContain("<<<<<<<", content);
    }

    // -------------------------------------------------------------------------
    // 16. Fields only present on one side (not a conflict — passthrough)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_FieldOnlyInTheirs_IncludesItInResult()
    {
        var ours = BaseMap();
        var theirs = BaseMap();
        theirs["onlyInTheirs"] = "some-value";

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("some-value", Get(merged!, "onlyInTheirs").Value<string>());
    }

    [Fact]
    public void TryMergeResolve_FieldOnlyInOurs_KeepsItInResult()
    {
        var ours = BaseMap();
        ours["onlyInOurs"] = "kept-value";
        var theirs = BaseMap();

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.True(resolved);
        Assert.Equal("kept-value", Get(merged!, "onlyInOurs").Value<string>());
    }

    // -------------------------------------------------------------------------
    // 17. Node identity mismatches → reject
    // -------------------------------------------------------------------------

    [Fact]
    public void TryMergeResolve_RootNodeUniqueIdentifiersDiffer_ReturnsFalse()
    {
        var ours = BaseMap();
        var theirs = BaseMap();
        theirs["rootNode"]!["uniqueIdentifier"] = "99999999-0000-0000-0000-000000000001";

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.False(resolved);
    }

    [Fact]
    public void TryMergeResolve_RootNodeTypesDiffer_ReturnsFalse()
    {
        var ours = BaseMap();
        var theirs = BaseMap();
        theirs["rootNode"]!["nodeType"] = 1;

        var resolved = MapConflictResolver.TryMergeResolve(Json(ours), Json(theirs), out var merged);

        Assert.False(resolved);
    }
}
