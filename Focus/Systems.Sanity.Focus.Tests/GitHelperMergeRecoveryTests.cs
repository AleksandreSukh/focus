using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

namespace Systems.Sanity.Focus.Tests;

public class GitHelperMergeRecoveryTests
{
    [Fact]
    public void TryRecoverResolvedFile_WhenNoMergeIsInProgress_ReturnsNoAction()
    {
        using var repository = new MergeRecoveryTestRepository();
        var filePath = repository.WriteFile("alpha.json", BuildMapJson("Alpha", "2026-04-20T08:00:00Z"));
        var executedCommands = new List<string[]>();
        var gitHelper = new GitHelper(
            repository.RootPath,
            executeGitCommand: (_, _, _, arguments) =>
            {
                executedCommands.Add(arguments);
                return (0, string.Empty, string.Empty);
            });

        var result = gitHelper.TryRecoverResolvedFile(filePath);

        Assert.Equal(MergeRecoveryStatus.NoAction, result.Status);
        Assert.Empty(executedCommands);
    }

    [Fact]
    public void TryRecoverResolvedFile_WhenFileIsOutsideRepository_ReturnsNoAction()
    {
        using var repository = new MergeRecoveryTestRepository();
        repository.EnableMerge();
        var outsideFilePath = Path.Combine(Path.GetTempPath(), "focus-tests", $"{Guid.NewGuid():N}.json");
        File.WriteAllText(outsideFilePath, BuildMapJson("Outside", "2026-04-20T08:00:00Z"));
        var executedCommands = new List<string[]>();
        var gitHelper = new GitHelper(
            repository.RootPath,
            executeGitCommand: (_, _, _, arguments) =>
            {
                executedCommands.Add(arguments);
                return (0, string.Empty, string.Empty);
            });

        try
        {
            var result = gitHelper.TryRecoverResolvedFile(outsideFilePath);

            Assert.Equal(MergeRecoveryStatus.NoAction, result.Status);
            Assert.Empty(executedCommands);
        }
        finally
        {
            if (File.Exists(outsideFilePath))
                File.Delete(outsideFilePath);
        }
    }

    [Fact]
    public void TryRecoverResolvedFile_WhenOtherUnmergedFilesRemain_StagesFileAndReturnsFileStaged()
    {
        using var repository = new MergeRecoveryTestRepository();
        repository.EnableMerge();
        var filePath = repository.WriteFile("alpha.json", BuildMapJson("Alpha", "2026-04-20T08:00:00Z"));
        var executedCommands = new List<string[]>();
        var unmergedFiles = new HashSet<string>(["alpha.json", "notes.txt"], StringComparer.OrdinalIgnoreCase);
        var gitHelper = new GitHelper(
            repository.RootPath,
            executeGitCommand: (_, _, _, arguments) =>
            {
                executedCommands.Add(arguments);
                return ExecuteMergeRecoveryCommand(arguments, unmergedFiles);
            });

        var result = gitHelper.TryRecoverResolvedFile(filePath);

        Assert.Equal(MergeRecoveryStatus.FileStaged, result.Status);
        Assert.Equal(["notes.txt"], result.RemainingUnmergedFiles);
        Assert.Contains(executedCommands, arguments => arguments.SequenceEqual(["add", "--", "alpha.json"]));
        Assert.DoesNotContain(executedCommands, arguments => arguments.SequenceEqual(["commit", "--no-edit"]));
    }

    [Fact]
    public void TryRecoverResolvedFile_WhenTargetIsLastUnmergedFile_CommitsMerge()
    {
        using var repository = new MergeRecoveryTestRepository();
        repository.EnableMerge();
        var filePath = repository.WriteFile("alpha.json", BuildMapJson("Alpha", "2026-04-20T08:00:00Z"));
        var executedCommands = new List<string[]>();
        var unmergedFiles = new HashSet<string>(["alpha.json"], StringComparer.OrdinalIgnoreCase);
        var gitHelper = new GitHelper(
            repository.RootPath,
            executeGitCommand: (_, _, _, arguments) =>
            {
                executedCommands.Add(arguments);
                return ExecuteMergeRecoveryCommand(arguments, unmergedFiles);
            });

        var result = gitHelper.TryRecoverResolvedFile(filePath);

        Assert.Equal(MergeRecoveryStatus.MergeCommitted, result.Status);
        Assert.Contains(executedCommands, arguments => arguments.SequenceEqual(["add", "--", "alpha.json"]));
        Assert.Contains(executedCommands, arguments => arguments.SequenceEqual(["commit", "--no-edit"]));
    }

    [Fact]
    public void TryRecoverResolvedFile_WhenFileStillHasConflictMarkers_DoesNotStageFile()
    {
        using var repository = new MergeRecoveryTestRepository();
        repository.EnableMerge();
        var filePath = repository.WriteFile(
            "alpha.json",
            BuildConflict(
                BuildMapJson("Root ours", "2026-04-20T08:00:00Z"),
                BuildMapJson("Root theirs", "2026-04-20T10:00:00Z")));
        var executedCommands = new List<string[]>();
        var unmergedFiles = new HashSet<string>(["alpha.json"], StringComparer.OrdinalIgnoreCase);
        var gitHelper = new GitHelper(
            repository.RootPath,
            executeGitCommand: (_, _, _, arguments) =>
            {
                executedCommands.Add(arguments);
                return ExecuteMergeRecoveryCommand(arguments, unmergedFiles);
            });

        var result = gitHelper.TryRecoverResolvedFile(filePath);

        Assert.Equal(MergeRecoveryStatus.UnresolvedFilesRemain, result.Status);
        Assert.Equal(["alpha.json"], result.RemainingUnmergedFiles);
        Assert.DoesNotContain(executedCommands, arguments => arguments[0] == "add");
        Assert.DoesNotContain(executedCommands, arguments => arguments.SequenceEqual(["commit", "--no-edit"]));
    }

    [Fact]
    public void PullLatestAtStartup_WhenUnmergedJsonAlreadyHasNoMarkers_StagesFileAndCommitsMerge()
    {
        using var repository = new MergeRecoveryTestRepository();
        repository.EnableMerge();
        repository.WriteFile("alpha.json", BuildMapJson("Alpha", "2026-04-20T08:00:00Z"));
        var executedCommands = new List<string[]>();
        var unmergedFiles = new HashSet<string>(["alpha.json"], StringComparer.OrdinalIgnoreCase);
        var gitHelper = new GitHelper(
            repository.RootPath,
            executeGitCommand: (_, _, _, arguments) =>
            {
                executedCommands.Add(arguments);
                return ExecuteMergeRecoveryCommand(arguments, unmergedFiles);
            });

        var result = gitHelper.PullLatestAtStartup();

        Assert.Equal(StartupSyncStatus.Succeeded, result.Status);
        Assert.Contains(executedCommands, arguments => arguments.SequenceEqual(["add", "--", "alpha.json"]));
        Assert.Contains(executedCommands, arguments => arguments.SequenceEqual(["commit", "--no-edit"]));
        Assert.Contains(executedCommands, arguments => arguments.SequenceEqual(["pull", "--no-rebase", "--quiet"]));
    }

    [Fact]
    public void PullLatestAtStartup_WhenAutoResolveSucceeds_StagesResolvedJsonAndCommitsMerge()
    {
        using var repository = new MergeRecoveryTestRepository();
        repository.EnableMerge();
        var filePath = repository.WriteFile(
            "alpha.json",
            BuildConflict(
                BuildMapJson("Root ours", "2026-04-20T08:00:00Z"),
                BuildMapJson("Root theirs", "2026-04-20T10:00:00Z")));
        var executedCommands = new List<string[]>();
        var unmergedFiles = new HashSet<string>(["alpha.json"], StringComparer.OrdinalIgnoreCase);
        var autoResolveCallCount = 0;
        var gitHelper = new GitHelper(
            repository.RootPath,
            executeGitCommand: (_, _, _, arguments) =>
            {
                executedCommands.Add(arguments);
                return ExecuteMergeRecoveryCommand(arguments, unmergedFiles);
            },
            autoResolveConflict: (absolutePath, _) =>
            {
                autoResolveCallCount++;
                File.WriteAllText(absolutePath, BuildMapJson("Resolved root", "2026-04-20T10:00:00Z"));
                return true;
            });

        var result = gitHelper.PullLatestAtStartup();

        Assert.Equal(StartupSyncStatus.Succeeded, result.Status);
        Assert.Equal(1, autoResolveCallCount);
        Assert.DoesNotContain("<<<<<<<", File.ReadAllText(filePath), StringComparison.Ordinal);
        Assert.Contains(executedCommands, arguments => arguments.SequenceEqual(["add", "--", "alpha.json"]));
        Assert.Contains(executedCommands, arguments => arguments.SequenceEqual(["commit", "--no-edit"]));
    }

    [Fact]
    public void PullLatestAtStartup_WhenUnresolvedFilesRemain_DoesNotCommitAndReturnsExplicitMessage()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();
        using var repository = new MergeRecoveryTestRepository();
        repository.EnableMerge();
        repository.WriteFile("alpha.json", "<<<<<<< HEAD\nnot json\n=======\nstill not json\n>>>>>>> abc123");
        repository.WriteFile("notes.txt", "needs manual merge");
        var executedCommands = new List<string[]>();
        var unmergedFiles = new HashSet<string>(["alpha.json", "notes.txt"], StringComparer.OrdinalIgnoreCase);
        var gitHelper = new GitHelper(
            repository.RootPath,
            executeGitCommand: (_, _, _, arguments) =>
            {
                executedCommands.Add(arguments);
                return ExecuteMergeRecoveryCommand(arguments, unmergedFiles);
            },
            autoResolveConflict: (_, _) => false);

        var result = gitHelper.PullLatestAtStartup();

        Assert.Equal(StartupSyncStatus.Failed, result.Status);
        Assert.Contains("alpha.json", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("notes.txt", result.ErrorMessage, StringComparison.Ordinal);
        Assert.NotEqual(ExceptionDiagnostics.BuildUserMessage("refreshing maps from git"), result.ErrorMessage);
        Assert.DoesNotContain(executedCommands, arguments => arguments.SequenceEqual(["commit", "--no-edit"]));
        Assert.DoesNotContain(executedCommands, arguments => arguments.SequenceEqual(["pull", "--no-rebase", "--quiet"]));
        Assert.Contains("Action: refreshing maps from git", diagnosticsScope.ReadLog(), StringComparison.Ordinal);
    }

    private static (int ExitCode, string StandardOutput, string StandardError) ExecuteMergeRecoveryCommand(
        IReadOnlyList<string> arguments,
        ISet<string> unmergedFiles)
    {
        if (arguments.SequenceEqual(["diff", "--name-only", "--diff-filter=U"]))
        {
            var output = unmergedFiles.Count == 0
                ? string.Empty
                : string.Join("\n", unmergedFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            return (0, output, string.Empty);
        }

        if (arguments.Count == 3 && arguments[0] == "add" && arguments[1] == "--")
        {
            unmergedFiles.Remove(arguments[2]);
            return (0, string.Empty, string.Empty);
        }

        if (arguments.SequenceEqual(["commit", "--no-edit"]) || arguments.SequenceEqual(["pull", "--no-rebase", "--quiet"]))
            return (0, string.Empty, string.Empty);

        throw new InvalidOperationException($"Unexpected git command: {string.Join(" ", arguments)}");
    }

    private static string BuildConflict(string ours, string theirs) =>
        $"<<<<<<< HEAD\n{ours}\n=======\n{theirs}\n>>>>>>> abc123";

    private static string BuildMapJson(string rootName, string updatedAtUtc) =>
        $$"""
        {
          "rootNode": {
            "nodeType": 0,
            "uniqueIdentifier": "11111111-1111-1111-1111-111111111111",
            "name": "{{rootName}}",
            "children": [],
            "links": {},
            "number": 1,
            "collapsed": false,
            "hideDoneTasks": false,
            "taskState": 0,
            "metadata": {
              "createdAtUtc": "2026-04-20T07:00:00Z",
              "updatedAtUtc": "{{updatedAtUtc}}",
              "source": "manual",
              "device": "focus-pwa-web",
              "attachments": []
            }
          },
          "updatedAt": "{{updatedAtUtc}}"
        }
        """;

    private sealed class MergeRecoveryTestRepository : IDisposable
    {
        public MergeRecoveryTestRepository()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "focus-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(Path.Combine(RootPath, ".git"));
        }

        public string RootPath { get; }

        public void EnableMerge()
        {
            File.WriteAllText(Path.Combine(RootPath, ".git", "MERGE_HEAD"), "abc123");
        }

        public string WriteFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var directoryPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
                Directory.CreateDirectory(directoryPath);

            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
