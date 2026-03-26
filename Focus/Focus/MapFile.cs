#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus
{
    public static class MapFile
    {
        public static MindMap OpenFile(string filePath, ISet<Guid>? usedIdentifiers = null)
        {
            var mindMapParsedFromJson =
                JsonConvert.DeserializeObject<MindMap>(File.ReadAllText(filePath)) ?? new MindMap();
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
