#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Systems.Sanity.Focus.Infrastructure.Diagnostics;

internal static class ExceptionDiagnostics
{
    private const string FallbackGlobalActionName = "running application";
    private static readonly object SyncRoot = new();
    private static string? _logFilePath;
    private static Action<string>? _userMessageWriter;
    private static bool _globalHandlersRegistered;

    public static void Initialize(string? logFilePath = null, Action<string>? userMessageWriter = null)
    {
        lock (SyncRoot)
        {
            _logFilePath = string.IsNullOrWhiteSpace(logFilePath)
                ? BuildDefaultLogFilePath()
                : logFilePath;
            _userMessageWriter = userMessageWriter;

            if (_globalHandlersRegistered)
                return;

            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
            _globalHandlersRegistered = true;
        }
    }

    internal static void ResetForTests(string? logFilePath = null, Action<string>? userMessageWriter = null)
    {
        lock (SyncRoot)
        {
            _logFilePath = string.IsNullOrWhiteSpace(logFilePath)
                ? BuildDefaultLogFilePath()
                : logFilePath;
            _userMessageWriter = userMessageWriter;
        }
    }

    internal static string BuildDefaultLogFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "focus-errors.log");

    public static string BuildUserMessage(string actionName) =>
        $"Error occured while {actionName}.";

    public static string LogException(Exception exception, string actionName)
    {
        TryAppendLogEntry(actionName, exception.ToString());
        return BuildUserMessage(actionName);
    }

    public static T Guard<T>(string actionName, Func<T> action, Func<string, T> onError)
    {
        try
        {
            return action();
        }
        catch (Exception exception)
        {
            return onError(LogException(exception, actionName));
        }
    }

    public static void Guard(string actionName, Action action, Action<string> onError)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            onError(LogException(exception, actionName));
        }
    }

    public static Task RunInBackgroundAsync(
        string actionName,
        Func<Task> action,
        Action<string>? onError = null)
    {
        return Task.Run(async () =>
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                ReportBackgroundException(exception, actionName, onError);
            }
        });
    }

    public static void ReportBackgroundException(
        Exception exception,
        string actionName,
        Action<string>? onError = null)
    {
        var userMessage = LogException(exception, actionName);
        TryNotifyUser(userMessage, onError);
    }

    private static void HandleUnhandledException(object? sender, UnhandledExceptionEventArgs args)
    {
        var exception = args.ExceptionObject as Exception
            ?? new InvalidOperationException(
                $"Unhandled exception object: {args.ExceptionObject}");

        ReportBackgroundException(exception, FallbackGlobalActionName);
    }

    private static void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        ReportBackgroundException(args.Exception, FallbackGlobalActionName);
        args.SetObserved();
    }

    private static void TryNotifyUser(string userMessage, Action<string>? onError)
    {
        try
        {
            var writer = onError ?? _userMessageWriter;
            if (writer != null)
            {
                writer(userMessage);
                return;
            }

            Console.Error.WriteLine(userMessage);
        }
        catch
        {
        }
    }

    private static void TryAppendLogEntry(string actionName, string exceptionText)
    {
        try
        {
            var logFilePath = _logFilePath;
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                logFilePath = BuildDefaultLogFilePath();
            }

            var directoryPath = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var timestamp = DateTimeOffset.Now;
            var builder = new StringBuilder();
            builder.AppendLine("============================================================");
            builder.Append("Timestamp: ").AppendLine(timestamp.ToString("O"));
            builder.Append("Action: ").AppendLine(actionName);
            builder.AppendLine("Exception:");
            builder.AppendLine(exceptionText);
            builder.AppendLine();

            lock (SyncRoot)
            {
                File.AppendAllText(logFilePath, builder.ToString());
            }
        }
        catch
        {
        }
    }
}
