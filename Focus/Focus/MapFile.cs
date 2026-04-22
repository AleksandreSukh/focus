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
            var attachmentStore = new MapAttachmentStore();
            var mindMapParsedFromJson =
                JsonConvert.DeserializeObject<MindMap>(
                    File.ReadAllText(filePath),
                    JsonSerialization.CreateDefaultSettings()) ?? new MindMap();
            var fileTimestampUtc = File.GetLastWriteTimeUtc(filePath);
            var normalizationResult = MapNormalizer.Normalize(
                mindMapParsedFromJson,
                usedIdentifiers,
                new DateTimeOffset(fileTimestampUtc, TimeSpan.Zero));
            if (normalizationResult.RemappedIdentifiers.Count > 0)
            {
                attachmentStore.MoveAttachmentDirectories(filePath, normalizationResult.RemappedIdentifiers);
            }

            if (normalizationResult.RequiresImmediateSave)
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
                var normalizationResult = MapNormalizer.Normalize(map);
                if (normalizationResult.RemappedIdentifiers.Count > 0)
                {
                    new MapAttachmentStore().MoveAttachmentDirectories(filePath, normalizationResult.RemappedIdentifiers);
                }
            }

            File.WriteAllText(
                filePath,
                JsonConvert.SerializeObject(map, JsonSerialization.CreateDefaultSettings()));
        }
    }
}
