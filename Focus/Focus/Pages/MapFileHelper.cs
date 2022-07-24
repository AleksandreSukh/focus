using System.IO;

namespace Systems.Sanity.Focus.Pages
{
    public class MapFileHelper
    {
        public static string GetFullFilePath(string directory, string fileName)
        {
            if (!fileName.EndsWith(".json")) //TODO
                fileName += ".json";

            var filePath = Path.Combine(directory, fileName);
            return filePath;
        }
    }
}