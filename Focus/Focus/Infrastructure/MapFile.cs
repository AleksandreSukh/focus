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
            mindMapParsedFromJson.LoadLinks(mindMapParsedFromJson.RootNode); //TODO
            return mindMapParsedFromJson;
        }
    }
}
