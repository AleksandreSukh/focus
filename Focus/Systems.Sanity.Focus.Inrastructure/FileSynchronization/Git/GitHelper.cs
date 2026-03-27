using System.ComponentModel;
using System.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization;

namespace Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

public class GitHelper
{
    private const string GitExecutableName = "git";
    private const string SyncCommitMessage = "auto update";

    private readonly object _gitSyncLock = new();
    private readonly string _gitRepositoryPath;
    private readonly Func<string, bool, bool, string[], (int ExitCode, string StandardOutput, string StandardError)> _executeGitCommand;

    public GitHelper(string gitRepositoryPath)
        : this(gitRepositoryPath, ExecuteGitCommand)
    {
    }

    internal GitHelper(
        string gitRepositoryPath,
        Func<string, bool, bool, string[], (int ExitCode, string StandardOutput, string StandardError)> executeGitCommand)
    {
        _gitRepositoryPath = gitRepositoryPath;
        _executeGitCommand = executeGitCommand;
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

    public void SynchronizeToRemote()
    {
        RunOnlyOneAtATime(CommitPullAndPush);
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
        Thread.Sleep(TimeSpan.FromSeconds(5));

        var consoleOldTitle = TryGetConsoleTitle();

        RunGitCommand("add", "--all");

        if (HasPendingChanges())
        {
            TrySetConsoleTitle("Syncing (committing changes)");
            RunGitCommand("commit", "-m", SyncCommitMessage);
        }

        TrySetConsoleTitle("Syncing (git pull)");
        RunGitCommand("pull", "--no-rebase", "--quiet");

        TrySetConsoleTitle("Syncing (git push)");
        RunGitCommand("push", "--quiet");

        if (!string.IsNullOrEmpty(consoleOldTitle))
            TrySetConsoleTitle(consoleOldTitle);
    }

    private void RunOnlyOneAtATime(Action syncAction)
    {
        Task.Run(() =>
        {
            if (!Monitor.TryEnter(_gitSyncLock))
                return;

            try
            {
                syncAction();
            }
            catch (Exception e)
            {
                ReportSyncFailure(e.Message);
            }
            finally
            {
                Monitor.Exit(_gitSyncLock);
            }
        });
    }

    private bool HasPendingChanges()
    {
        var result = _executeGitCommand(_gitRepositoryPath, true, true, ["status", "--porcelain"]);
        return !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    private void RunGitCommand(params string[] arguments)
    {
        _executeGitCommand(_gitRepositoryPath, false, true, arguments);
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

    private static void ReportSyncFailure(string message)
    {
        if (OperatingSystem.IsWindows())
        {
            TrySetConsoleTitle($"Syncing failed - {message}");
            return;
        }

        Console.Error.WriteLine($"Git sync failed: {message}");
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
