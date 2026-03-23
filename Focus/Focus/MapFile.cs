using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus
{
    public class MapFile
    {
        public static MindMap OpenFile(string filePath, ISet<Guid> usedIdentifiers = null)
        {
            var mindMapParsedFromJson = JsonConvert.DeserializeObject<MindMap>(File.ReadAllText(filePath)) ?? new MindMap();
            var normalizationResult = MapNormalizer.Normalize(mindMapParsedFromJson, usedIdentifiers);
            if (normalizationResult.WasChanged)
            {
                Save(filePath, mindMapParsedFromJson, normalizeMap: false);
            }

            return mindMapParsedFromJson;
        }

        public static void Save(string filePath, MindMap map)
        {
            Save(filePath, map, normalizeMap: true);
        }

        public static void RebuildNodeIndex(IEnumerable<string> filePaths)
        {
            GlobalLinkDitionary.Nodes.Clear();
            GlobalLinkDitionary.NodeFiles.Clear();
            GlobalLinkDitionary.Backlinks.Clear();
            var usedIdentifiers = new HashSet<Guid>();
            foreach (var filePath in filePaths)
            {
                var map = OpenFile(filePath, usedIdentifiers);
                IndexMap(map, filePath);
            }
        }

        private static void IndexMap(MindMap map, string mapFilePath)
        {
            IndexNode(map.RootNode, mapFilePath);
        }

        private static void IndexNode(Node node, string mapFilePath)
        {
            if (node.UniqueIdentifier.HasValue && node.UniqueIdentifier.Value != Guid.Empty)
            {
                var nodeIdentifier = node.UniqueIdentifier.Value;
                GlobalLinkDitionary.Nodes[nodeIdentifier] = node;
                GlobalLinkDitionary.NodeFiles[nodeIdentifier] = mapFilePath;

                foreach (var link in node.Links.Values)
                {
                    if (!GlobalLinkDitionary.Backlinks.TryGetValue(link.id, out var backlinkIdentifiers))
                    {
                        backlinkIdentifiers = new HashSet<Guid>();
                        GlobalLinkDitionary.Backlinks[link.id] = backlinkIdentifiers;
                    }

                    backlinkIdentifiers.Add(nodeIdentifier);
                }
            }

            foreach (var childNode in node.Children)
            {
                IndexNode(childNode, mapFilePath);
            }
        }

        private static void Save(string filePath, MindMap map, bool normalizeMap)
        {
            if (normalizeMap)
            {
                MapNormalizer.Normalize(map);
            }

            File.WriteAllText(filePath, JsonConvert.SerializeObject(map));
        }
    }
}
