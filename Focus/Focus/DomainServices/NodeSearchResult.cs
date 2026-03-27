using System;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal sealed record NodeSearchResult(
    Guid NodeId,
    string NodeName,
    string NodePath,
    string MapFilePath,
    string MapName,
    int Score,
    int Depth,
    string ContextLabel = null,
    TaskState TaskState = TaskState.None)
{
    public string ToDisplayString(bool includeMapName)
    {
        var locationPrefix = includeMapName ? $"{MapName}: " : string.Empty;
        var contextSuffix = string.IsNullOrWhiteSpace(ContextLabel)
            ? string.Empty
            : $" [{ContextLabel}]";
        var displayPath = NodeDisplayHelper.PrefixTaskMarker(NodePath, TaskState);

        return locationPrefix + displayPath + contextSuffix;
    }
}
