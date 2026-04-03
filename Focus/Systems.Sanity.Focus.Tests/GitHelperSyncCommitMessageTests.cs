using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

namespace Systems.Sanity.Focus.Tests;

public class GitHelperSyncCommitMessageTests
{
    [Fact]
    public void Synchronize_UsesSuppliedCommitMessage()
    {
        using var pushCompleted = new ManualResetEventSlim(false);
        string? commitMessage = null;
        var handler = new FileSynchronizationHandlerGit(
            new GitHelper(
                gitRepositoryPath: @"C:\repo",
                executeGitCommand: (_, _, _, arguments) =>
                {
                    if (arguments.SequenceEqual(["status", "--porcelain"]))
                        return (0, "M alpha.json", string.Empty);

                    if (arguments.Length >= 3 && arguments[0] == "commit")
                        commitMessage = arguments[^1];

                    if (arguments[0] == "push")
                        pushCompleted.Set();

                    return (0, string.Empty, string.Empty);
                },
                waitForSynchronizationDelay: _ => { }));

        handler.Synchronize("Hide node in alpha");

        Assert.True(pushCompleted.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal("Hide node in alpha", commitMessage);
    }

    [Fact]
    public void SynchronizeToRemote_BatchesDistinctPendingMessages()
    {
        using var releaseDelay = new ManualResetEventSlim(false);
        using var pushCompleted = new ManualResetEventSlim(false);
        string? commitMessage = null;
        var gitHelper = new GitHelper(
            gitRepositoryPath: @"C:\repo",
            executeGitCommand: (_, _, _, arguments) =>
            {
                if (arguments.SequenceEqual(["status", "--porcelain"]))
                    return (0, "M alpha.json", string.Empty);

                if (arguments.Length >= 3 && arguments[0] == "commit")
                    commitMessage = arguments[^1];

                if (arguments[0] == "push")
                    pushCompleted.Set();

                return (0, string.Empty, string.Empty);
            },
            waitForSynchronizationDelay: _ => releaseDelay.Wait(TimeSpan.FromSeconds(2)));

        gitHelper.SynchronizeToRemote("Add note in alpha");
        gitHelper.SynchronizeToRemote("Hide node in alpha");
        releaseDelay.Set();

        Assert.True(pushCompleted.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal("Batch update: Add note in alpha; Hide node in alpha", commitMessage);
    }

    [Fact]
    public void SynchronizeToRemote_DeduplicatesIdenticalPendingMessages()
    {
        using var releaseDelay = new ManualResetEventSlim(false);
        using var pushCompleted = new ManualResetEventSlim(false);
        string? commitMessage = null;
        var gitHelper = new GitHelper(
            gitRepositoryPath: @"C:\repo",
            executeGitCommand: (_, _, _, arguments) =>
            {
                if (arguments.SequenceEqual(["status", "--porcelain"]))
                    return (0, "M alpha.json", string.Empty);

                if (arguments.Length >= 3 && arguments[0] == "commit")
                    commitMessage = arguments[^1];

                if (arguments[0] == "push")
                    pushCompleted.Set();

                return (0, string.Empty, string.Empty);
            },
            waitForSynchronizationDelay: _ => releaseDelay.Wait(TimeSpan.FromSeconds(2)));

        gitHelper.SynchronizeToRemote("Hide node in alpha");
        gitHelper.SynchronizeToRemote("Hide node in alpha");
        releaseDelay.Set();

        Assert.True(pushCompleted.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal("Hide node in alpha", commitMessage);
    }

    [Fact]
    public void SynchronizeToRemote_KeepsMessagesQueuedDuringActiveSyncForNextCommit()
    {
        using var firstCommitRecorded = new ManualResetEventSlim(false);
        using var releaseFirstPush = new ManualResetEventSlim(false);
        using var secondPushCompleted = new ManualResetEventSlim(false);
        var commitMessages = new List<string>();
        var pushCount = 0;
        var gitHelper = new GitHelper(
            gitRepositoryPath: @"C:\repo",
            executeGitCommand: (_, _, _, arguments) =>
            {
                if (arguments.SequenceEqual(["status", "--porcelain"]))
                    return (0, "M alpha.json", string.Empty);

                if (arguments.Length >= 3 && arguments[0] == "commit")
                {
                    lock (commitMessages)
                    {
                        commitMessages.Add(arguments[^1]);
                        if (commitMessages.Count == 1)
                            firstCommitRecorded.Set();
                    }
                }

                if (arguments[0] == "push")
                {
                    var currentPushCount = Interlocked.Increment(ref pushCount);
                    if (currentPushCount == 1)
                    {
                        releaseFirstPush.Wait(TimeSpan.FromSeconds(2));
                    }
                    else
                    {
                        secondPushCompleted.Set();
                    }
                }

                return (0, string.Empty, string.Empty);
            },
            waitForSynchronizationDelay: _ => { });

        gitHelper.SynchronizeToRemote("Hide node in alpha");

        Assert.True(firstCommitRecorded.Wait(TimeSpan.FromSeconds(2)));
        gitHelper.SynchronizeToRemote("Edit node in alpha");
        releaseFirstPush.Set();
        Assert.True(secondPushCompleted.Wait(TimeSpan.FromSeconds(2)));

        lock (commitMessages)
        {
            Assert.Equal(["Hide node in alpha", "Edit node in alpha"], commitMessages);
        }
    }

    [Fact]
    public void SynchronizeToRemote_DoesNotCommitWhenGitHasNoPendingChanges()
    {
        using var pushCompleted = new ManualResetEventSlim(false);
        var commitMessages = new List<string>();
        var gitHelper = new GitHelper(
            gitRepositoryPath: @"C:\repo",
            executeGitCommand: (_, _, _, arguments) =>
            {
                if (arguments.SequenceEqual(["status", "--porcelain"]))
                    return (0, string.Empty, string.Empty);

                if (arguments.Length >= 3 && arguments[0] == "commit")
                    commitMessages.Add(arguments[^1]);

                if (arguments[0] == "push")
                    pushCompleted.Set();

                return (0, string.Empty, string.Empty);
            },
            waitForSynchronizationDelay: _ => { });

        gitHelper.SynchronizeToRemote("Hide node in alpha");

        Assert.True(pushCompleted.Wait(TimeSpan.FromSeconds(2)));
        Assert.Empty(commitMessages);
    }

    [Fact]
    public void SynchronizeToRemote_CapturesSyncCommandOutputWhenBackgroundNotifierIsProvided()
    {
        using var pushCompleted = new ManualResetEventSlim(false);
        var commandCaptureModes = new List<(string Command, bool CaptureOutput)>();
        var backgroundMessages = new List<string>();
        var gitHelper = new GitHelper(
            gitRepositoryPath: @"C:\repo",
            executeGitCommand: (_, captureOutput, _, arguments) =>
            {
                lock (commandCaptureModes)
                {
                    commandCaptureModes.Add((arguments[0], captureOutput));
                }

                if (arguments.SequenceEqual(["status", "--porcelain"]))
                    return (0, "M alpha.json", string.Empty);

                if (arguments[0] == "commit")
                    return (0, "[autosync 1234567] Hide node in alpha", string.Empty);

                if (arguments[0] == "push")
                    pushCompleted.Set();

                return (0, string.Empty, string.Empty);
            },
            waitForSynchronizationDelay: _ => { },
            writeBackgroundMessage: message => backgroundMessages.Add(message));

        gitHelper.SynchronizeToRemote("Hide node in alpha");

        Assert.True(pushCompleted.Wait(TimeSpan.FromSeconds(2)));

        lock (commandCaptureModes)
        {
            Assert.Contains(("add", true), commandCaptureModes);
            Assert.Contains(("commit", true), commandCaptureModes);
            Assert.Contains(("pull", true), commandCaptureModes);
            Assert.Contains(("push", true), commandCaptureModes);
            Assert.Contains(("status", true), commandCaptureModes);
        }

        Assert.Contains("[autosync 1234567] Hide node in alpha", backgroundMessages);
    }

    [Fact]
    public void SynchronizeToRemote_ReportsFailureThroughBackgroundNotifier()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        using var failureReported = new ManualResetEventSlim(false);
        string? reportedMessage = null;
        var gitHelper = new GitHelper(
            gitRepositoryPath: @"C:\repo",
            executeGitCommand: (_, _, _, arguments) =>
            {
                if (arguments.SequenceEqual(["status", "--porcelain"]))
                    return (0, "M alpha.json", string.Empty);

                if (arguments[0] == "push")
                    throw new InvalidOperationException("remote rejected the push");

                return (0, string.Empty, string.Empty);
            },
            waitForSynchronizationDelay: _ => { },
            writeBackgroundMessage: message =>
            {
                reportedMessage = message;
                failureReported.Set();
            });

        gitHelper.SynchronizeToRemote("Hide node in alpha");

        Assert.True(failureReported.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(ExceptionDiagnostics.BuildUserMessage("synchronizing changes to git"), reportedMessage);
        Assert.Contains("Action: synchronizing changes to git", diagnosticsScope.ReadLog());
        Assert.Contains("remote rejected the push", diagnosticsScope.ReadLog());
    }
}
