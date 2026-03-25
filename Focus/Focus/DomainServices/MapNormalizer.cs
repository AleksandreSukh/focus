using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.DomainServices;

internal static class MapNormalizer
{
    public static MapNormalizationResult Normalize(MindMap map, ISet<Guid> usedIdentifiers = null)
    {
        usedIdentifiers ??= new HashSet<Guid>();

        var normalizationResult = new MapNormalizationResult();
        var remappedIdentifiers = new Dictionary<Guid, Guid>();
        var currentNodeIdentifier = map.GetCurrentNodeIdentifier();

        NormalizeNode(map.RootNode, null, usedIdentifiers, remappedIdentifiers, normalizationResult);
        RewriteLinkTargets(map.RootNode, remappedIdentifiers, normalizationResult);

        if (currentNodeIdentifier.HasValue &&
            remappedIdentifiers.TryGetValue(currentNodeIdentifier.Value, out var remappedCurrentNodeIdentifier))
        {
            currentNodeIdentifier = remappedCurrentNodeIdentifier;
        }

        if (!currentNodeIdentifier.HasValue || !map.ChangeCurrentNodeById(currentNodeIdentifier.Value))
        {
            map.ResetCurrentNodeToRoot();
        }

        return normalizationResult;
    }

    private static void NormalizeNode(
        Node node,
        Node parentNode,
        ISet<Guid> usedIdentifiers,
        IDictionary<Guid, Guid> remappedIdentifiers,
        MapNormalizationResult normalizationResult)
    {
        node.SetParent(parentNode);

        if (node.SanitizeName())
        {
            normalizationResult.SanitizedNodeNamesCount++;
        }

        var existingIdentifier = node.UniqueIdentifier;
        if (!existingIdentifier.HasValue || existingIdentifier.Value == Guid.Empty)
        {
            node.UniqueIdentifier = GenerateUniqueIdentifier(usedIdentifiers);
            normalizationResult.AddedIdentifiersCount++;
        }
        else if (!usedIdentifiers.Add(existingIdentifier.Value))
        {
            var remappedIdentifier = GenerateUniqueIdentifier(usedIdentifiers);
            remappedIdentifiers.TryAdd(existingIdentifier.Value, remappedIdentifier);
            node.UniqueIdentifier = remappedIdentifier;
            normalizationResult.RepairedDuplicateIdentifiersCount++;
        }

        foreach (var childNode in node.Children)
        {
            NormalizeNode(childNode, node, usedIdentifiers, remappedIdentifiers, normalizationResult);
        }
    }

    private static void RewriteLinkTargets(
        Node node,
        IReadOnlyDictionary<Guid, Guid> remappedIdentifiers,
        MapNormalizationResult normalizationResult)
    {
        if (node.Links.Count > 0)
        {
            var rewrittenLinks = new Dictionary<Guid, Link>();
            foreach (var link in node.Links.Values)
            {
                var targetIdentifier = link.id;
                if (remappedIdentifiers.TryGetValue(targetIdentifier, out var remappedIdentifier))
                {
                    targetIdentifier = remappedIdentifier;
                    normalizationResult.UpdatedLinkReferencesCount++;
                }

                rewrittenLinks[targetIdentifier] = new Link(targetIdentifier, link.relationType, link.metadata);
            }

            node.Links.Clear();
            foreach (var rewrittenLink in rewrittenLinks)
            {
                node.Links.Add(rewrittenLink.Key, rewrittenLink.Value);
            }
        }

        foreach (var childNode in node.Children)
        {
            RewriteLinkTargets(childNode, remappedIdentifiers, normalizationResult);
        }
    }

    private static Guid GenerateUniqueIdentifier(ISet<Guid> usedIdentifiers)
    {
        Guid candidateIdentifier;
        do
        {
            candidateIdentifier = Guid.NewGuid();
        }
        while (!usedIdentifiers.Add(candidateIdentifier));

        return candidateIdentifier;
    }
}
