using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.E2E.Tests;

internal sealed class GitSandbox : IDisposable
{
    public GitSandbox(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        RemoteDirectory = Path.Combine(rootDirectory, "origin.git");
        WorkingDirectory = Path.Combine(rootDirectory, "working");
        CollaboratorDirectory = Path.Combine(rootDirectory, "collaborator");

        Directory.CreateDirectory(rootDirectory);
        Directory.CreateDirectory(WorkingDirectory);

        RunGit(rootDirectory, "init", "--bare", "--initial-branch", "main", RemoteDirectory);

        RunGit(WorkingDirectory, "init", "--initial-branch", "main");
        ConfigureIdentity(WorkingDirectory);
        RunGit(WorkingDirectory, "remote", "add", "origin", RemoteDirectory);
        RunGit(WorkingDirectory, "commit", "--allow-empty", "-m", "Initial commit");
        RunGit(WorkingDirectory, "push", "-u", "origin", "main");

        RunGit(rootDirectory, "clone", RemoteDirectory, CollaboratorDirectory);
        ConfigureIdentity(CollaboratorDirectory);
    }

    public string RootDirectory { get; }

    public string RemoteDirectory { get; }

    public string WorkingDirectory { get; }

    public string CollaboratorDirectory { get; }

    public string WorkingMapsDirectory => Path.Combine(WorkingDirectory, "FocusMaps");

    public string CollaboratorMapsDirectory => Path.Combine(CollaboratorDirectory, "FocusMaps");

    public void WriteWorkingMap(string fileName, MindMap map)
    {
        Directory.CreateDirectory(WorkingMapsDirectory);
        MapFile.Save(Path.Combine(WorkingMapsDirectory, NormalizeJsonFileName(fileName)), map);
    }

    public void WriteCollaboratorMap(string fileName, MindMap map)
    {
        Directory.CreateDirectory(CollaboratorMapsDirectory);
        MapFile.Save(Path.Combine(CollaboratorMapsDirectory, NormalizeJsonFileName(fileName)), map);
    }

    public void CommitAndPushCollaborator(string commitMessage)
    {
        RunGit(CollaboratorDirectory, "add", "--all");
        RunGit(CollaboratorDirectory, "commit", "-m", commitMessage);
        RunGit(CollaboratorDirectory, "push", "origin", "main");
    }

    public string GetRemoteHeadCommitMessage()
    {
        return RunGit(
                RootDirectory,
                $"--git-dir={RemoteDirectory}",
                "log",
                "-1",
                "--pretty=%B")
            .StandardOutput
            .Trim();
    }

    public string GetWorkingHeadCommitMessage()
    {
        return RunGit(WorkingDirectory, "log", "-1", "--pretty=%B").StandardOutput.Trim();
    }

    public string GetCollaboratorHeadCommitMessage()
    {
        return RunGit(CollaboratorDirectory, "log", "-1", "--pretty=%B").StandardOutput.Trim();
    }

    public IReadOnlyList<string> GetWorkingHeadCommitMessages(int count)
    {
        return GetCommitMessages(WorkingDirectory, count);
    }

    public IReadOnlyList<string> GetRemoteHeadCommitMessages(int count)
    {
        return GetCommitMessages(RootDirectory, count, $"--git-dir={RemoteDirectory}");
    }

    public void SetWorkingRemoteToMissingPath()
    {
        var missingRemoteDirectory = Path.Combine(RootDirectory, "missing-origin.git");
        RunGit(WorkingDirectory, "remote", "set-url", "origin", missingRemoteDirectory);
    }

    public void Dispose()
    {
        TestDirectoryCleaner.DeleteDirectory(RootDirectory);
    }

    private static void ConfigureIdentity(string workingDirectory)
    {
        RunGit(workingDirectory, "config", "user.name", "Focus E2E");
        RunGit(workingDirectory, "config", "user.email", "focus-e2e@example.com");
    }

    private static GitCommandResult RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        var result = new GitCommandResult(process.ExitCode, standardOutput, standardError);
        if (result.ExitCode != 0)
        {
            var commandText = string.Join(" ", arguments);
            var errorMessage = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput.Trim()
                : result.StandardError.Trim();
            throw new InvalidOperationException(
                $"git {commandText} failed with exit code {result.ExitCode}: {errorMessage}");
        }

        return result;
    }

    private static IReadOnlyList<string> GetCommitMessages(string workingDirectory, int count, params string[] commandPrefix)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");

        var arguments = commandPrefix
            .Concat(["log", $"-{count}", "--pretty=%s"])
            .ToArray();
        var output = RunGit(workingDirectory, arguments).StandardOutput;

        return output
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(message => message.Trim())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();
    }

    private static string NormalizeJsonFileName(string fileName)
    {
        return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.json";
    }

    private sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
}
