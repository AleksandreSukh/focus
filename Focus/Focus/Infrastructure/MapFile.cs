﻿using System;
using System.IO;
using Newtonsoft.Json;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Infrastructure
{
    public class MapFile
    {
        public static MindMap OpenFile(string filePath)
        {
            var mindMapParsedFromJson = JsonConvert.DeserializeObject<MindMap>(File.ReadAllText(filePath));
            return mindMapParsedFromJson;
        }

        public static void LoadLinks(string filePath)
        {
            var mindMapParsedFromJson = JsonConvert.DeserializeObject<MindMap>(File.ReadAllText(filePath));
            LoadLinks(mindMapParsedFromJson.RootNode); //TODO
        }

        private static void LoadLinks(Node node)
        {
            node.UniqueIdentifier ??= Guid.NewGuid();
            GlobalLinkDitionary.Nodes.Add(node.UniqueIdentifier.Value, node);
            foreach (var childNode in node.Children)
            {
                LoadLinks(childNode);
            }
        }
    }
}
