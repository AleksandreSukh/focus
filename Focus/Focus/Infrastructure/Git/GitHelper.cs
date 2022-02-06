using System;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace Systems.Sanity.Focus.Infrastructure.Git
{
    public class GitHelper
    {
        public static void SyncronizeToRemote(string gitRepositoryName)
        {
            var repository = new Repository(gitRepositoryName);
            var config = repository.Config;
            var author = config.BuildSignature(DateTimeOffset.Now);
            if (repository.Diff.Compare<TreeChanges>().Count > 0)
            {
                Commands.Stage(repository, "*");
                repository.Commit("auto update",
                    author, author,
                    new CommitOptions());
            }

            var creds = CredentialStoreHelper.GetCredentialsFromMicrosoftCredentialStore();
            CredentialsHandler credentialsHandler = (_url, _user, _cred) => creds;

            Commands.Pull(repository, author, new PullOptions()
            {
                FetchOptions = new FetchOptions()
                {
                    CredentialsProvider = credentialsHandler
                },
                MergeOptions = new MergeOptions()
            });

            repository.Network.Push(
                repository.Network.Remotes[repository.Head.RemoteName],
                repository.Head.UpstreamBranchCanonicalName,
                new PushOptions() { CredentialsProvider = credentialsHandler });
        }
    }
}
