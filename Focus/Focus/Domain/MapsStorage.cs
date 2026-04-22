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
            : this(userConfig, CreateFileSyncHandler(userConfig.GitRepository, GitSynchronizationOptions.BackgroundDebounced))
        {
        }

        internal MapsStorage(UserConfig userConfig, GitSynchronizationOptions gitSynchronizationOptions)
            : this(userConfig, CreateFileSyncHandler(userConfig.GitRepository, gitSynchronizationOptions))
        {
        }

        internal MapsStorage(UserConfig userConfig, IFileSynchronizationHandler fileSynchronizationHandler)
        {
            UserMindMapsDirectory = Path.Combine(userConfig.DataFolder, ConfigurationConstants.MindMapDirectoryName);
            GitRepositoryPath = userConfig.GitRepository;
            _fileSynchronizationHandler = fileSynchronizationHandler;
            AttachmentStore = new MapAttachmentStore();
        }

        public string UserMindMapsDirectory { get; }

        public string GitRepositoryPath { get; }

        internal MapAttachmentStore AttachmentStore { get; }

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

        public MindMap OpenMapForEditing(string filePath)
        {
            var fileContent = File.ReadAllText(filePath);
            if (!HasConflictMarkers(fileContent))
            {
                _fileSynchronizationHandler.TryRecoverResolvedFile(filePath);
                return MapFile.OpenFile(filePath);
            }

            if (!MapConflictResolver.TryResolve(fileContent, out var resolvedContent) || string.IsNullOrWhiteSpace(resolvedContent))
                throw new MapConflictAutoResolveException();

            File.WriteAllText(filePath, resolvedContent);
            _fileSynchronizationHandler.TryRecoverResolvedFile(filePath);
            return MapFile.OpenFile(filePath);
        }

        public void SaveMap(string filePath, MindMap map)
        {
            MapFile.Save(filePath, map);
            _fileSynchronizationHandler.TryRecoverResolvedFile(filePath);
        }

        public void Sync(string commitMessage)
        {
            if (string.IsNullOrWhiteSpace(commitMessage))
                throw new ArgumentException("Sync commit message is required.", nameof(commitMessage));

            _fileSynchronizationHandler.Synchronize(commitMessage);
        }

        public StartupSyncResult PullLatestAtStartup()
        {
            return _fileSynchronizationHandler.PullLatestAtStartup();
        }

        private static IFileSynchronizationHandler CreateFileSyncHandler(
            string gitRepositoryPath,
            GitSynchronizationOptions gitSynchronizationOptions)
        {
            if (GitHelper.IsRepositoryAvailable(gitRepositoryPath))
            {
                var gitHelper = new GitHelper(
                    gitRepositoryPath,
                    message => AppConsole.Current.WriteBackgroundMessage(message),
                    gitSynchronizationOptions,
                    (absoluteFilePath, conflictedContent) =>
                    {
                        if (!MapConflictResolver.TryResolve(conflictedContent, out var resolved))
                            return false;
                        File.WriteAllText(absoluteFilePath, resolved!);
                        return true;
                    });
                return new FileSynchronizationHandlerGit(gitHelper);
            }

            return new FileSynchronizationHandlerEmpty();
        }

        private static bool HasConflictMarkers(string content) =>
            content.Contains("<<<<<<< ", StringComparison.Ordinal);
    }
}
