using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;

namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

public class GitHelper
{
    private const string GitExecutableName = "git";
    private const string FallbackSyncCommitMessage = "Update files";

    private readonly object _gitSyncLock = new();
    private readonly object _syncStateLock = new();
    private readonly Queue<string> _pendingCommitMessages = new();
    private readonly string _gitRepositoryPath;
    private readonly Func<string, bool, bool, string[], (int ExitCode, string StandardOutput, string StandardError)> _executeGitCommand;
    private readonly Action<string>? _writeBackgroundMessage;
    private readonly Action<TimeSpan> _waitForSynchronizationDelay;
    private bool _syncWorkerRunning;

    public GitHelper(string gitRepositoryPath)
        : this(gitRepositoryPath, ExecuteGitCommand, Thread.Sleep, null)
    {
    }

    public GitHelper(string gitRepositoryPath, Action<string>? writeBackgroundMessage)
        : this(gitRepositoryPath, ExecuteGitCommand, Thread.Sleep, writeBackgroundMessage)
    {
    }

    internal GitHelper(
        string gitRepositoryPath,
        Func<string, bool, bool, string[], (int ExitCode, string StandardOutput, string StandardError)> executeGitCommand,
        Action<TimeSpan>? waitForSynchronizationDelay = null,
        Action<string>? writeBackgroundMessage = null)
    {
        _gitRepositoryPath = gitRepositoryPath;
        _executeGitCommand = executeGitCommand;
        _waitForSynchronizationDelay = waitForSynchronizationDelay ?? Thread.Sleep;
        _writeBackgroundMessage = writeBackgroundMessage;
    }

    public static bool IsRepositoryAvailable(string gitRepositoryPath)
    {
        if (string.IsNullOrWhiteSpace(gitRepositoryPath) || !Directory.Exists(gitRepositoryPath))
            return false;

        try
        {
            var result = ExecuteGitCommand(
                gitRepositoryPath,
                captureOutput: true,
                throwOnFailure: false,
                "rev-parse",
                "--is-inside-work-tree");
            return result.ExitCode == 0 &&
                   string.Equals(result.StandardOutput.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void SynchronizeToRemote(string commitMessage)
    {
        if (string.IsNullOrWhiteSpace(commitMessage))
            throw new ArgumentException("Sync commit message is required.", nameof(commitMessage));

        lock (_syncStateLock)
        {
            _pendingCommitMessages.Enqueue(commitMessage.Trim());
            if (_syncWorkerRunning)
                return;

            _syncWorkerRunning = true;
        }

        Task.Run(ProcessPendingSynchronizations);
    }

    public StartupSyncResult PullLatestAtStartup()
    {
        lock (_gitSyncLock)
        {
            var consoleOldTitle = TryGetConsoleTitle();

            try
            {
                TrySetConsoleTitle("Syncing (git pull)");
                RunGitCommand("pull", "--no-rebase", "--quiet");
                return StartupSyncResult.Succeeded;
            }
            catch (Exception e)
            {
                return StartupSyncResult.Failed(e.Message);
            }
            finally
            {
                if (!string.IsNullOrEmpty(consoleOldTitle))
                    TrySetConsoleTitle(consoleOldTitle);
            }
        }
    }

    private void CommitPullAndPush()
    {
        var consoleOldTitle = TryGetConsoleTitle();
        try
        {
            var pendingMessageCount = GetPendingCommitMessageCount();

            RunBackgroundGitCommand("add", "--all");

            if (HasPendingChanges())
            {
                var commitMessage = FormatCommitMessage(DrainPendingCommitMessages(pendingMessageCount));
                TrySetConsoleTitle("Syncing (committing changes)");
                RunBackgroundGitCommand("commit", "-m", commitMessage);
            }
            else
            {
                DrainPendingCommitMessages(pendingMessageCount);
            }

            TrySetConsoleTitle("Syncing (git pull)");
            RunBackgroundGitCommand("pull", "--no-rebase", "--quiet");

            TrySetConsoleTitle("Syncing (git push)");
            RunBackgroundGitCommand("push", "--quiet");
        }
        finally
        {
            if (!string.IsNullOrEmpty(consoleOldTitle))
                TrySetConsoleTitle(consoleOldTitle);
        }
    }

    private void ProcessPendingSynchronizations()
    {
        while (true)
        {
            _waitForSynchronizationDelay(TimeSpan.FromSeconds(5));

            try
            {
                lock (_gitSyncLock)
                {
                    CommitPullAndPush();
                }
            }
            catch (Exception e)
            {
                ReportSyncFailure(e.Message, _writeBackgroundMessage);
                lock (_syncStateLock)
                {
                    _syncWorkerRunning = false;
                }

                return;
            }

            lock (_syncStateLock)
            {
                if (_pendingCommitMessages.Count > 0)
                    continue;

                _syncWorkerRunning = false;
                return;
            }
        }
    }

    private bool HasPendingChanges()
    {
        var result = _executeGitCommand(_gitRepositoryPath, true, true, ["status", "--porcelain"]);
        return !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    private int GetPendingCommitMessageCount()
    {
        lock (_syncStateLock)
        {
            return _pendingCommitMessages.Count;
        }
    }

    private IReadOnlyList<string> DrainPendingCommitMessages(int messageCount)
    {
        var messages = new List<string>(Math.Max(0, messageCount));

        lock (_syncStateLock)
        {
            while (messageCount-- > 0 && _pendingCommitMessages.Count > 0)
            {
                messages.Add(_pendingCommitMessages.Dequeue());
            }
        }

        return messages;
    }

    private static string FormatCommitMessage(IReadOnlyList<string> messages)
    {
        var uniqueMessages = messages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => message.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (uniqueMessages.Count == 0)
            return FallbackSyncCommitMessage;

        if (uniqueMessages.Count == 1)
            return uniqueMessages[0];

        const int maxMessagesInSummary = 2;
        var displayedMessages = uniqueMessages.Take(maxMessagesInSummary).ToArray();
        var remainingMessageCount = uniqueMessages.Count - displayedMessages.Length;
        var summary = string.Join("; ", displayedMessages);

        return remainingMessageCount > 0
            ? $"Batch update: {summary}; +{remainingMessageCount} more"
            : $"Batch update: {summary}";
    }

    private void RunGitCommand(params string[] arguments)
    {
        _executeGitCommand(_gitRepositoryPath, false, true, arguments);
    }

    private void RunBackgroundGitCommand(params string[] arguments)
    {
        if (_writeBackgroundMessage == null)
        {
            RunGitCommand(arguments);
            return;
        }

        var result = _executeGitCommand(_gitRepositoryPath, true, true, arguments);
        ReportBackgroundCommandOutput(result, _writeBackgroundMessage);
    }

    private static (int ExitCode, string StandardOutput, string StandardError) ExecuteGitCommand(
        string workingDirectory,
        bool captureOutput,
        bool throwOnFailure,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GitExecutableName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (captureOutput)
        {
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the git process.");

            string standardOutput = string.Empty;
            string standardError = string.Empty;

            if (captureOutput)
            {
                standardOutput = process.StandardOutput.ReadToEnd();
                standardError = process.StandardError.ReadToEnd();
            }

            process.WaitForExit();

            var result = (process.ExitCode, standardOutput, standardError);
            if (throwOnFailure && result.ExitCode != 0)
                throw CreateCommandException(arguments, result);

            return result;
        }
        catch (Win32Exception e)
        {
            throw new InvalidOperationException(
                $"The git executable was not found. Make sure \"{GitExecutableName}\" is installed and available on PATH.",
                e);
        }
    }

    private static InvalidOperationException CreateCommandException(
        string[] arguments,
        (int ExitCode, string StandardOutput, string StandardError) result)
    {
        var errorMessage = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        var commandText = string.Join(" ", arguments);

        if (string.IsNullOrWhiteSpace(errorMessage))
            errorMessage = $"git {commandText} failed with exit code {result.ExitCode}.";
        else
            errorMessage = $"git {commandText} failed with exit code {result.ExitCode}: {errorMessage}";

        return new InvalidOperationException(errorMessage);
    }

    private static void ReportBackgroundCommandOutput(
        (int ExitCode, string StandardOutput, string StandardError) result,
        Action<string> writeBackgroundMessage)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            writeBackgroundMessage(result.StandardOutput.TrimEnd('\r', '\n'));

        if (!string.IsNullOrWhiteSpace(result.StandardError))
            writeBackgroundMessage(result.StandardError.TrimEnd('\r', '\n'));
    }

    private static void ReportSyncFailure(string message, Action<string>? writeBackgroundMessage)
    {
        var failureMessage = $"Git sync failed: {message}";

        writeBackgroundMessage?.Invoke(failureMessage);

        if (OperatingSystem.IsWindows())
        {
            TrySetConsoleTitle($"Syncing failed - {message}");
            return;
        }

        if (writeBackgroundMessage == null)
            Console.Error.WriteLine(failureMessage);
    }

    private static string? TryGetConsoleTitle()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            return Console.Title;
        }
        catch
        {
            return null;
        }
    }

    private static void TrySetConsoleTitle(string title)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            Console.Title = title;
        }
        catch
        {
        }
    }
}
