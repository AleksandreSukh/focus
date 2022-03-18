using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
