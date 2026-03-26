#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

namespace Systems.Sanity.Focus.Domain
{
    public class MapsStorage : IMapRepository
    {
        private readonly IFileSynchronizationHandler _fileSynchronizationHandler;

        public MapsStorage(UserConfig userConfig)
        {
            UserMindMapsDirectory = Path.Combine(userConfig.DataFolder, ConfigurationConstants.MindMapDirectoryName);
            GitRepositoryPath = userConfig.GitRepository;
            _fileSynchronizationHandler = InitFileSyncHandler(GitRepositoryPath);
        }

        public string UserMindMapsDirectory { get; }

        public string GitRepositoryPath { get; }

        public void DeleteMap(FileInfo file)
        {
            file.Delete();
        }

        public FileInfo[] GetAll()
        {
            var existingMapDir = new DirectoryInfo(UserMindMapsDirectory);
            if (!existingMapDir.Exists)
                return Array.Empty<FileInfo>();

            return existingMapDir.GetFiles($"*{ConfigurationConstants.RequiredFileNameExtension}")
                .OrderBy(file => file.Name)
                .ToArray();
        }

        public FileInfo[] GetTop(int top)
        {
            var existingMapDir = new DirectoryInfo(UserMindMapsDirectory);
            if (!existingMapDir.Exists)
                return Array.Empty<FileInfo>();

            return existingMapDir.GetFiles($"*{ConfigurationConstants.RequiredFileNameExtension}")
                .OrderByDescending(file => file.LastAccessTimeUtc)
                .Take(top)
                .ToArray();
        }

        public void MoveMap(string existingFilePath, string newFilePath)
        {
            File.Move(existingFilePath, newFilePath);
        }

        public MindMap OpenMap(string filePath, ISet<Guid>? usedIdentifiers = null)
        {
            return MapFile.OpenFile(filePath, usedIdentifiers);
        }

        public void SaveMap(string filePath, MindMap map)
        {
            MapFile.Save(filePath, map);
        }

        public void Sync()
        {
            _fileSynchronizationHandler.Synchronize();
        }

        private IFileSynchronizationHandler InitFileSyncHandler(string gitRepositoryPath)
        {
            if (GitHelper.IsRepositoryAvailable(gitRepositoryPath))
            {
                var gitHelper = new GitHelper(gitRepositoryPath);
                return new FileSynchronizationHandlerGit(gitHelper);
            }

            return new FileSynchronizationHandlerEmpty();
        }
    }
}
