using System;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace Systems.Sanity.Focus.Infrastructure.Git
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

        public void SyncronizeToRemote()
        {
            RunOnlyOneAtATime(CommitPullAndPush);
        }

        private void CommitPullAndPush()
        {
            Thread.Sleep(5000);
            var consoleOldTitle = Console.Title;
            Commands.Stage(_repository, "*");

            var thereAreUnsavedLocalChanges = _repository.Diff.Compare<TreeChanges>().Count > 0;

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

            Commands.Pull(_repository, _author, new PullOptions()
            {
                FetchOptions = new FetchOptions() { CredentialsProvider = _credentialsHandler },
                MergeOptions = new MergeOptions()
            });

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
