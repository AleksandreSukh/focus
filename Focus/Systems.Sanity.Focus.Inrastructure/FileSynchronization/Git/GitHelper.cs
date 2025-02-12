﻿using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Console = System.Console; 

namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git
{
    public class GitHelper
    {
        private const string SyncCommitMessage = "auto update";

        private readonly object _gitSyncLock = new();

        private readonly Repository _repository;
        private readonly Signature _author;
        private readonly CredentialsHandler _credentialsHandler;

        public GitHelper(string gitRepositoryName)
        {
            _repository = new Repository(gitRepositoryName);
            _author = _repository.Config.BuildSignature(DateTimeOffset.Now);
            var credenitals = CredentialStoreHelper.GetCredentialsFromMicrosoftCredentialStore();
            _credentialsHandler = (_, _, _) => credenitals;
        }

        public async Task SyncronizeToRemote()
        {
            RunOnlyOneAtATime(CommitPullAndPush);
        }

        private void CommitPullAndPush()
        {
            Thread.Sleep(5000);
            var consoleOldTitle = Console.Title; //TODO: move to consoleTitleWriter infrastructure class
            Commands.Stage(_repository, "*");

            var thereAreUnsavedLocalChanges = _repository.RetrieveStatus().IsDirty;

            if (thereAreUnsavedLocalChanges)
            {
                Console.Title = "Syncing (committing changes)";

                try
                {
                    _repository.Commit(SyncCommitMessage, _author, _author, new CommitOptions());
                }
                catch (EmptyCommitException e)
                {
                    //TODO: Log hidden errors into file
                }
            }

            Console.Title = "Syncing (git pull)";

            try
            {
                Commands.Pull(_repository, _author, new PullOptions()
                {
                    FetchOptions = new FetchOptions() { CredentialsProvider = _credentialsHandler },
                    MergeOptions = new MergeOptions()
                });
            }
            catch (CheckoutConflictException e)
            {
                Console.Title = $"Syncing failed - {e.Message}";
                throw;
            }


            Console.Title = "Syncing (git push)";

            _repository.Network.Push(
                _repository.Network.Remotes[_repository.Head.RemoteName],
                _repository.Head.UpstreamBranchCanonicalName,
                new PushOptions() { CredentialsProvider = _credentialsHandler });

            Console.Title = consoleOldTitle;
        }

        private void RunOnlyOneAtATime(Action syncAction)
        {
            Task.Run(() =>
            {
                if (!Monitor.TryEnter(_gitSyncLock)) return;
                try
                {
                    syncAction();
                }
                finally
                {
                    Monitor.Exit(_gitSyncLock);
                }
            });
        }
    }
}
