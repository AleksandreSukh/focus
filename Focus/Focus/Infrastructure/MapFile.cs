using System.IO;
using Newtonsoft.Json;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Infrastructure
{
    public class MapFile
    {
        public static MindMap OpenFile(string filePath)
        {
            return JsonConvert.DeserializeObject<MindMap>(File.ReadAllText(filePath));
        }
    }
}
