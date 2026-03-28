namespace Systems.Sanity.Focus.DomainServices;

internal sealed class MapNormalizationResult
{
    public int AddedIdentifiersCount { get; set; }
    public int RepairedDuplicateIdentifiersCount { get; set; }
    public int UpdatedLinkReferencesCount { get; set; }
    public int SanitizedNodeNamesCount { get; set; }
    public int BackfilledMetadataCount { get; set; }

    public bool WasChanged =>
        RequiresImmediateSave ||
        BackfilledMetadataCount > 0;

    public bool RequiresImmediateSave =>
        AddedIdentifiersCount > 0 ||
        RepairedDuplicateIdentifiersCount > 0 ||
        UpdatedLinkReferencesCount > 0 ||
        SanitizedNodeNamesCount > 0;
}
