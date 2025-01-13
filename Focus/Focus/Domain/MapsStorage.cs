using System;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

namespace Systems.Sanity.Focus.Domain
{
    public class MapsStorage
    {
        const string MapsFolderName = "FocusMaps"; //TODO: make configurable

        private readonly IFileSynchronizationHandler _fileSynchronizationHandler;

        public MapsStorage(UserConfig userConfig)
        {
            UserMindMapsDirectory = Path.Combine(userConfig.DataFolder, MapsFolderName); //TODO: improve naming
            GitRepositoryPath = userConfig.GitRepository;
            _fileSynchronizationHandler = InitFileSyncHandler(GitRepositoryPath);
        }

        private IFileSynchronizationHandler InitFileSyncHandler(string gitRepositoryPath)
        {
            bool isWindows = OsInfo.IsWindows();
            if (isWindows)
            {
                if (!string.IsNullOrWhiteSpace(gitRepositoryPath))
                {
                    var gitHelper = new GitHelper(gitRepositoryPath);
                    return new FileSynchronizationHandlerGit(gitHelper);
                }
            }
            return new FileSynchronizationHandlerEmpty();
        }

        public string UserMindMapsDirectory { get; }
        public string GitRepositoryPath { get; }

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

        public void Sync()
        {
            _fileSynchronizationHandler.Synchronize();
        }
    }
}