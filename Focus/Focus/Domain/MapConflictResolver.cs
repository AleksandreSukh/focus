#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Systems.Sanity.Focus.Domain;

public static class MapConflictResolver
{
    private const string LegacySource = "legacy-import";

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    public static bool TryResolve(string conflictedContent, out string? resolvedContent)
    {
        resolvedContent = null;

        if (!HasConflictMarkers(conflictedContent))
            return false;

        var ours = BuildResolvedContent(conflictedContent, takeOurs: true);
        var theirs = BuildResolvedContent(conflictedContent, takeOurs: false);

        // Phase A: field-level merge — preserves changes from both sides when possible
        if (TryMergeResolve(ours, theirs, out resolvedContent))
            return true;

        // Phase B: whole-document timestamp selection — fallback when Phase A cannot classify a difference
        var oursTimestamp = TryParseMapTimestamp(ours);
        var theirsTimestamp = TryParseMapTimestamp(theirs);

        if (oursTimestamp == null && theirsTimestamp == null)
            return false;

        // Theirs wins on tie; ours wins only if strictly newer.
        resolvedContent = oursTimestamp > theirsTimestamp ? ours : theirs;
        return true;
    }

    // -------------------------------------------------------------------------
    // Phase A — field-level merge
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to merge two clean (conflict-marker-free) JSON map documents
    /// field by field using known safe rules. Returns false when any differing
    /// field cannot be classified, signalling Phase B should be used instead.
    /// </summary>
    internal static bool TryMergeResolve(string oursJson, string theirsJson, out string? mergedJson)
    {
        mergedJson = null;
        try
        {
            // Parse without date handling so timestamp strings stay as raw strings
            // rather than being converted to DateTimeOffset and re-serialized in locale format.
            var ours = LoadJson(oursJson);
            var theirs = LoadJson(theirsJson);

            if (!TryMergeMapDocument(ours, theirs))
                return false;

            mergedJson = ours.ToString(Formatting.Indented);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static JObject LoadJson(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json))
        {
            DateParseHandling = DateParseHandling.None
        };
        return JObject.Load(reader);
    }

    private static bool TryMergeMapDocument(JObject ours, JObject theirs)
    {
        foreach (var key in GetAllKeys(ours, theirs))
        {
            var oursVal = ours[key];
            var theirsVal = theirs[key];

            if (JToken.DeepEquals(oursVal, theirsVal)) continue;
            if (oursVal == null) { ours[key] = theirsVal!.DeepClone(); continue; }  // only in theirs
            if (theirsVal == null) continue;                                          // only in ours

            switch (key)
            {
                case "updatedAt":
                    MergeTimestampMax(ours, theirs, "updatedAt");
                    break;

                case "rootNode":
                    if (oursVal is not JObject oursRoot || theirsVal is not JObject theirsRoot)
                        return false;
                    if (!TryMergeNode(oursRoot, theirsRoot))
                        return false;
                    break;

                default:
                    return false; // unknown differing top-level field
            }
        }

        return true;
    }

    private static bool TryMergeNode(JObject ours, JObject theirs)
    {
        // Identity fields must be the same — different GUIDs or types mean a structural mismatch
        if (!JToken.DeepEquals(ours["uniqueIdentifier"], theirs["uniqueIdentifier"]))
            return false;
        if (!JToken.DeepEquals(ours["nodeType"], theirs["nodeType"]))
            return false;

        // Determine recency once, used for all NewerNodeWins fields
        var oursUpdated = TryParseTimestamp(ours["metadata"]?["updatedAtUtc"]?.Value<string>());
        var theirsUpdated = TryParseTimestamp(theirs["metadata"]?["updatedAtUtc"]?.Value<string>());
        var theirsIsNewerOrEqual = theirsUpdated >= oursUpdated;

        foreach (var key in GetAllKeys(ours, theirs))
        {
            var oursVal = ours[key];
            var theirsVal = theirs[key];

            if (JToken.DeepEquals(oursVal, theirsVal)) continue;
            if (oursVal == null) { ours[key] = theirsVal!.DeepClone(); continue; }
            if (theirsVal == null) continue;

            switch (key)
            {
                case "uniqueIdentifier":
                case "nodeType":
                    return false; // should have been caught above; guard against reachability

                case "number":
                    // Renumbering is handled by the parent's children merge; keep ours
                    break;

                // Last-write-wins: whichever node has the newer updatedAtUtc takes all of these
                case "name":
                case "collapsed":
                case "hideDoneTasks":
                case "taskState":
                    if (theirsIsNewerOrEqual)
                        ours[key] = theirsVal.DeepClone();
                    break;

                case "metadata":
                    if (oursVal is not JObject oursMeta || theirsVal is not JObject theirsMeta)
                        return false;
                    if (!TryMergeMetadata(oursMeta, theirsMeta, theirsIsNewerOrEqual))
                        return false;
                    break;

                case "links":
                    if (oursVal is not JObject oursLinks || theirsVal is not JObject theirsLinks)
                        return false;
                    MergeLinksUnion(oursLinks, theirsLinks);
                    break;

                case "children":
                    if (oursVal is not JArray oursChildren || theirsVal is not JArray theirsChildren)
                        return false;
                    if (!TryMergeChildren(oursChildren, theirsChildren))
                        return false;
                    break;

                default:
                    return false; // unknown differing node field
            }
        }

        return true;
    }

    private static bool TryMergeMetadata(JObject ours, JObject theirs, bool theirsIsNewerOrEqual)
    {
        foreach (var key in GetAllKeys(ours, theirs))
        {
            var oursVal = ours[key];
            var theirsVal = theirs[key];

            if (JToken.DeepEquals(oursVal, theirsVal)) continue;
            if (oursVal == null) { ours[key] = theirsVal!.DeepClone(); continue; }
            if (theirsVal == null) continue;

            switch (key)
            {
                case "updatedAtUtc":
                    MergeTimestampMax(ours, theirs, "updatedAtUtc");
                    break;

                case "createdAtUtc":
                    // Creation time is immutable — the earlier value is always more correct
                    MergeTimestampMin(ours, theirs, "createdAtUtc");
                    break;

                case "source":
                {
                    var oursSource = oursVal.Value<string>();
                    var theirsSource = theirsVal.Value<string>();
                    // Never regress to "legacy-import" once a real source is set.
                    // Otherwise follow recency.
                    var takeTheirs = (oursSource == LegacySource && theirsSource != LegacySource)
                                  || (theirsIsNewerOrEqual && theirsSource != LegacySource);
                    if (takeTheirs)
                        ours["source"] = theirsVal.DeepClone();
                    break;
                }

                case "device":
                    if (theirsIsNewerOrEqual)
                        ours["device"] = theirsVal.DeepClone();
                    break;

                case "attachments":
                    if (oursVal is not JArray oursAtt || theirsVal is not JArray theirsAtt)
                        return false;
                    MergeAttachmentsUnion(oursAtt, theirsAtt);
                    break;

                default:
                    return false; // unknown differing metadata field
            }
        }

        return true;
    }

    // -------------------------------------------------------------------------
    // Collection merge helpers
    // -------------------------------------------------------------------------

    private static bool TryMergeChildren(JArray oursChildren, JArray theirsChildren)
    {
        // Index ours children by uniqueIdentifier for O(1) lookup
        var oursById = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in oursChildren)
        {
            if (child is not JObject obj) return false;
            var id = obj["uniqueIdentifier"]?.Value<string>();
            if (string.IsNullOrEmpty(id)) return false; // cannot match without GUID
            oursById[id] = obj;
        }

        // Walk theirs in order: merge shared children in-place, append theirs-only at the end
        foreach (var child in theirsChildren)
        {
            if (child is not JObject obj) return false;
            var id = obj["uniqueIdentifier"]?.Value<string>();
            if (string.IsNullOrEmpty(id)) return false;

            if (oursById.TryGetValue(id, out var oursChild))
            {
                if (!TryMergeNode(oursChild, obj))
                    return false;
            }
            else
            {
                oursChildren.Add(obj.DeepClone());
            }
        }

        // Renumber sequentially (ours order first, then appended theirs-only children)
        var number = 1;
        foreach (var child in oursChildren)
        {
            if (child is JObject obj)
                obj["number"] = number++;
        }

        return true;
    }

    private static void MergeLinksUnion(JObject oursLinks, JObject theirsLinks)
    {
        // Links are keyed by target-node GUID; union is safe because GUIDs prevent collisions
        foreach (var prop in theirsLinks.Properties())
        {
            if (oursLinks[prop.Name] == null)
                oursLinks.Add(prop.Name, prop.Value.DeepClone());
        }
    }

    private static void MergeAttachmentsUnion(JArray oursAttachments, JArray theirsAttachments)
    {
        // Attachments carry a stable `id` GUID; union by that id avoids duplicates
        var oursIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var att in oursAttachments)
        {
            if (att is JObject obj)
            {
                var id = obj["id"]?.Value<string>();
                if (id != null) oursIds.Add(id);
            }
        }

        foreach (var att in theirsAttachments)
        {
            if (att is JObject obj)
            {
                var id = obj["id"]?.Value<string>();
                if (id != null && !oursIds.Contains(id))
                    oursAttachments.Add(obj.DeepClone());
            }
        }
    }

    // -------------------------------------------------------------------------
    // Scalar merge helpers
    // -------------------------------------------------------------------------

    private static void MergeTimestampMax(JObject ours, JObject theirs, string field)
    {
        var oursTs = TryParseTimestamp(ours[field]?.Value<string>());
        var theirsTs = TryParseTimestamp(theirs[field]?.Value<string>());
        if (theirsTs > oursTs)
            ours[field] = theirs[field]!.DeepClone();
    }

    private static void MergeTimestampMin(JObject ours, JObject theirs, string field)
    {
        var oursTs = TryParseTimestamp(ours[field]?.Value<string>());
        var theirsTs = TryParseTimestamp(theirs[field]?.Value<string>());
        if (theirsTs != null && theirsTs < oursTs)
            ours[field] = theirs[field]!.DeepClone();
    }

    // -------------------------------------------------------------------------
    // Phase B — whole-document timestamp selection (unchanged from original)
    // -------------------------------------------------------------------------

    private static DateTimeOffset? TryParseMapTimestamp(string? jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            return null;

        try
        {
            var obj = JObject.Parse(jsonContent);

            // Top-level updatedAt (camelCase, from PWA / MindMap.UpdatedAt)
            var updatedAt = obj["updatedAt"];
            if (updatedAt != null && updatedAt.Type != JTokenType.Null)
            {
                if (DateTimeOffset.TryParse(updatedAt.Value<string>(), out var ts))
                    return ts;
            }

            // Node metadata: rootNode.metadata.updatedAtUtc (console app)
            var rootNodeMetadata = obj["rootNode"]?["metadata"];
            if (rootNodeMetadata != null)
            {
                var updatedAtUtc = rootNodeMetadata["updatedAtUtc"];
                if (updatedAtUtc != null && updatedAtUtc.Type != JTokenType.Null)
                {
                    if (DateTimeOffset.TryParse(updatedAtUtc.Value<string>(), out var ts))
                        return ts;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Conflict marker stripping
    // -------------------------------------------------------------------------

    internal static string BuildResolvedContent(string content, bool takeOurs)
    {
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n');
        var result = new List<string>(lines.Length);
        var inOurs = false;
        var inTheirs = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("<<<<<<< ", StringComparison.Ordinal))
            {
                inOurs = true;
                inTheirs = false;
            }
            else if (line == "=======")
            {
                inOurs = false;
                inTheirs = true;
            }
            else if (line.StartsWith(">>>>>>> ", StringComparison.Ordinal))
            {
                inOurs = false;
                inTheirs = false;
            }
            else if (!inOurs && !inTheirs)
            {
                result.Add(line);
            }
            else if (takeOurs && inOurs)
            {
                result.Add(line);
            }
            else if (!takeOurs && inTheirs)
            {
                result.Add(line);
            }
        }

        return string.Join("\n", result);
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private static bool HasConflictMarkers(string content) =>
        content.Contains("<<<<<<< ", StringComparison.Ordinal);

    private static DateTimeOffset? TryParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTimeOffset.TryParse(value, out var ts) ? ts : null;
    }

    private static HashSet<string> GetAllKeys(JObject ours, JObject theirs)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in ours.Properties()) keys.Add(p.Name);
        foreach (var p in theirs.Properties()) keys.Add(p.Name);
        return keys;
    }
}
