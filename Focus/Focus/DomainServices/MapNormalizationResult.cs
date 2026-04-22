using System;
using System.Collections.Generic;

namespace Systems.Sanity.Focus.DomainServices;

internal sealed class MapNormalizationResult
{
    private readonly Dictionary<Guid, Guid> _remappedIdentifiers = new();

    public int AddedIdentifiersCount { get; set; }
    public int RepairedDuplicateIdentifiersCount { get; set; }
    public int UpdatedLinkReferencesCount { get; set; }
    public int SanitizedNodeNamesCount { get; set; }
    public int BackfilledMetadataCount { get; set; }
    public IReadOnlyDictionary<Guid, Guid> RemappedIdentifiers => _remappedIdentifiers;

    public bool WasChanged =>
        RequiresImmediateSave ||
        BackfilledMetadataCount > 0;

    public bool RequiresImmediateSave =>
        AddedIdentifiersCount > 0 ||
        RepairedDuplicateIdentifiersCount > 0 ||
        UpdatedLinkReferencesCount > 0 ||
        SanitizedNodeNamesCount > 0;

    public void TrackRemappedIdentifier(Guid existingIdentifier, Guid remappedIdentifier)
    {
        _remappedIdentifiers.TryAdd(existingIdentifier, remappedIdentifier);
    }
}
