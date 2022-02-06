using System;
using System.IO;
using System.Linq;

namespace Systems.Sanity.Focus
{
    public class MapsStorage
    {
        const string MapsFolderName = "FocusMaps"; //TODO

        public MapsStorage(UserConfig userConfig)
        {
            UserMindMapsDirectory = Path.Combine(userConfig.DataFolder, MapsFolderName);
            GitRepository = userConfig.GitRepository;
        }

        public string UserMindMapsDirectory { get; }
        public string GitRepository { get; }

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