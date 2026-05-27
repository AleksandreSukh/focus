#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Application.Llm;

internal interface ILlmAgentClient
{
    LlmAgentResponse Run(LlmAgentRequest request);
}

internal sealed record LlmAgentRequest(
    string AgentName,
    string Prompt,
    string ContextMarkdown,
    string ContextJson,
    string WorkingDirectory);

internal sealed record LlmAgentResponse(bool IsSuccess, string? Answer, string? ErrorMessage)
{
    public static LlmAgentResponse Success(string answer) => new(true, answer, null);

    public static LlmAgentResponse Error(string errorMessage) => new(false, null, errorMessage);
}

internal sealed class CodexLlmAgentClient : ILlmAgentClient
{
    private const int DefaultTimeoutSeconds = 600;

    private readonly LlmConfig? _config;

    public CodexLlmAgentClient(LlmConfig? config)
    {
        _config = config;
    }

    public LlmAgentResponse Run(LlmAgentRequest request)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"focus-codex-{Guid.NewGuid():N}.txt");
        try
        {
            return RunInternal(request, outputPath);
        }
        finally
        {
            TryDelete(outputPath);
        }
    }

    private LlmAgentResponse RunInternal(LlmAgentRequest request, string outputPath)
    {
        try
        {
            var codexCommand = BuildProcessCommand(GetCodexCommand());
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = codexCommand.FileName,
                WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                    ? Environment.CurrentDirectory
                    : request.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };

            foreach (var argument in codexCommand.PrefixArguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            foreach (var argument in BuildArguments(request, outputPath))
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            process.StandardInput.Write(BuildPrompt(request));
            process.StandardInput.Close();

            var timeoutSeconds = GetTimeoutSeconds();
            var timeoutMilliseconds = (int)Math.Min(
                int.MaxValue,
                TimeSpan.FromSeconds(timeoutSeconds).TotalMilliseconds);
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                TryKill(process);
                return LlmAgentResponse.Error($"Codex did not finish within {timeoutSeconds} seconds.");
            }

            Task.WaitAll(stdoutTask, stderrTask);
            var stdout = stdoutTask.Result;
            var stderr = stderrTask.Result;
            if (process.ExitCode != 0)
                return LlmAgentResponse.Error(BuildProcessError(process.ExitCode, stdout, stderr));

            if (!File.Exists(outputPath))
                return LlmAgentResponse.Error("Codex did not write a final answer.");

            var answer = File.ReadAllText(outputPath, Encoding.UTF8).Trim();
            return string.IsNullOrWhiteSpace(answer)
                ? LlmAgentResponse.Error("Codex returned an empty answer.")
                : LlmAgentResponse.Success(answer);
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
        {
            return LlmAgentResponse.Error($"Couldn't run Codex: {ex.Message}");
        }
    }

    private IEnumerable<string> BuildArguments(LlmAgentRequest request, string outputPath)
    {
        yield return "exec";
        yield return "--sandbox";
        yield return "read-only";
        yield return "--color";
        yield return "never";
        yield return "--ephemeral";
        yield return "--skip-git-repo-check";
        yield return "--cd";
        yield return request.WorkingDirectory;
        yield return "--output-last-message";
        yield return outputPath;

        if (!string.IsNullOrWhiteSpace(_config?.CodexModel))
        {
            yield return "--model";
            yield return _config!.CodexModel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_config?.CodexProfile))
        {
            yield return "--profile";
            yield return _config!.CodexProfile.Trim();
        }

        yield return "-";
    }

    private static string BuildPrompt(LlmAgentRequest request) =>
        string.Join(Environment.NewLine, new[]
        {
            "You are answering a Focus mindmap prompt.",
            "Do not edit files, run commands that change files, or write side effects.",
            "Use the provided Focus context only as reference material.",
            "Return only the answer text that should be appended to the mindmap.",
            string.Empty,
            $"Prompt: {request.Prompt}",
            string.Empty,
            "Focus context (Markdown):",
            request.ContextMarkdown,
            string.Empty,
            "Focus context (JSON):",
            request.ContextJson,
            string.Empty
        });

    private string GetCodexCommand() =>
        string.IsNullOrWhiteSpace(_config?.CodexCommand)
            ? "codex"
            : _config!.CodexCommand.Trim();

    internal static CodexProcessCommand BuildProcessCommand(string command, string? pathValue = null)
    {
        var executablePath = ResolveExecutablePath(command, pathValue);
        var extension = Path.GetExtension(executablePath);
        if (OperatingSystem.IsWindows() &&
            (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            return new CodexProcessCommand("cmd.exe", new[] { "/d", "/s", "/c", executablePath });
        }

        return new CodexProcessCommand(executablePath, Array.Empty<string>());
    }

    internal static string ResolveExecutablePath(string command, string? pathValue = null)
    {
        var normalizedCommand = string.IsNullOrWhiteSpace(command)
            ? "codex"
            : command.Trim();

        if (HasDirectorySegment(normalizedCommand))
            return ResolveCandidatePath(normalizedCommand) ?? normalizedCommand;

        var searchPath = pathValue ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidateBase = Path.Combine(directory, normalizedCommand);
            var resolved = ResolveCandidatePath(candidateBase);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }

        return normalizedCommand;
    }

    private static string? ResolveCandidatePath(string candidateBase)
    {
        foreach (var candidate in EnumerateExecutableCandidates(candidateBase))
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    private static IEnumerable<string> EnumerateExecutableCandidates(string candidateBase)
    {
        var extension = Path.GetExtension(candidateBase);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            yield return candidateBase;
            yield break;
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var extensionCandidate in GetWindowsExecutableExtensions())
                yield return candidateBase + extensionCandidate;
        }

        yield return candidateBase;
    }

    private static IReadOnlyList<string> GetWindowsExecutableExtensions()
    {
        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        var extensions = (pathExt ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(extension => extension.StartsWith(".", StringComparison.Ordinal) ? extension : $".{extension}")
            .ToList();

        foreach (var extension in new[] { ".COM", ".EXE", ".BAT", ".CMD" })
        {
            if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                extensions.Add(extension);
        }

        return extensions;
    }

    private static bool HasDirectorySegment(string command) =>
        command.Contains(Path.DirectorySeparatorChar) ||
        command.Contains(Path.AltDirectorySeparatorChar);

    private int GetTimeoutSeconds()
    {
        var configured = _config?.CodexTimeoutSeconds;
        return configured.HasValue && configured.Value > 0
            ? configured.Value
            : DefaultTimeoutSeconds;
    }

    private static string BuildProcessError(int exitCode, string stdout, string stderr)
    {
        var detail = !string.IsNullOrWhiteSpace(stderr)
            ? stderr.Trim()
            : stdout.Trim();
        return string.IsNullOrWhiteSpace(detail)
            ? $"Codex exited with code {exitCode}."
            : $"Codex exited with code {exitCode}: {detail}";
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
        }
    }
}

internal sealed record CodexProcessCommand(string FileName, IReadOnlyList<string> PrefixArguments);
