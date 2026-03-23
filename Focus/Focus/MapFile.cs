using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus
{
    public class MapFile
    {
        public static MindMap OpenFile(string filePath)
        {
            var mindMapParsedFromJson = JsonConvert.DeserializeObject<MindMap>(File.ReadAllText(filePath));
            LoadLinks(mindMapParsedFromJson.RootNode);
            LoadMissingLinkedNodes(filePath, mindMapParsedFromJson.RootNode);
            return mindMapParsedFromJson;
        }

        public static void LoadLinks(string filePath)
        {
            var mindMapParsedFromJson = JsonConvert.DeserializeObject<MindMap>(File.ReadAllText(filePath));
            LoadLinks(mindMapParsedFromJson.RootNode);
        }

        private static void LoadLinks(Node node)
        {
            node.UniqueIdentifier ??= Guid.NewGuid();
            GlobalLinkDitionary.Nodes[node.UniqueIdentifier.Value] = node;
            foreach (var childNode in node.Children)
            {
                LoadLinks(childNode);
            }
        }

        private static void LoadMissingLinkedNodes(string filePath, Node rootNode)
        {
            var missingLinkedNodeIds = new HashSet<Guid>();
            CollectMissingLinkedNodeIds(rootNode, missingLinkedNodeIds);
            if (missingLinkedNodeIds.Count == 0)
                return;

            var directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                return;

            foreach (var mapFilePath in Directory.EnumerateFiles(directoryPath, $"*{ConfigurationConstants.RequiredFileNameExtension}"))
            {
                if (string.Equals(mapFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                LoadLinks(mapFilePath);
                missingLinkedNodeIds.RemoveWhere(linkId => GlobalLinkDitionary.Nodes.ContainsKey(linkId));
                if (missingLinkedNodeIds.Count == 0)
                    return;
            }
        }

        private static void CollectMissingLinkedNodeIds(Node node, HashSet<Guid> missingLinkedNodeIds)
        {
            foreach (var linkId in node.Links.Keys)
            {
                if (!GlobalLinkDitionary.Nodes.ContainsKey(linkId))
                {
                    missingLinkedNodeIds.Add(linkId);
                }
            }

            foreach (var childNode in node.Children)
            {
                CollectMissingLinkedNodeIds(childNode, missingLinkedNodeIds);
            }
        }
    }
}
