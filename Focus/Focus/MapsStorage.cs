using System;
using System.IO;
using System.Linq;

namespace Systems.Sanity.Focus
{
    public class MapsStorage
    {
        public MapsStorage(string userMindMapsDirectory)
        {
            UserMindMapsDirectory = userMindMapsDirectory;
        }

        //TODO - naming
        public string UserMindMapsDirectory { get; }

        public FileInfo[] GetTop(int top)
        {
            var existingMapDir = new DirectoryInfo(UserMindMapsDirectory);
            if (!existingMapDir.Exists)
                return Array.Empty<FileInfo>();
            return existingMapDir.GetFiles("*.json")
                .OrderByDescending(f => f.LastAccessTimeUtc)
                .Take(top)
                .ToArray();
        }
    }
}