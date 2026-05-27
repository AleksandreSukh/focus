#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Systems.Sanity.Focus.Application.Llm;

internal sealed class LlmJobStore
{
    private const string JobsDirectoryName = "jobs";
    private const string LlmDirectoryName = "_llm";

    private readonly string _mapsDirectory;

    public LlmJobStore(string mapsDirectory)
    {
        _mapsDirectory = Path.GetFullPath(mapsDirectory);
    }

    public string JobsDirectory => Path.Combine(_mapsDirectory, LlmDirectoryName, JobsDirectoryName);

    public LlmJobEntry CreateJob(string mapFilePath, Guid nodeId, string prompt)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var job = Normalize(new LlmJob
        {
            Id = Guid.NewGuid().ToString("D"),
            Status = LlmJobStatus.Pending,
            Mode = LlmPromptService.ContextMode,
            MapFilePath = BuildProtocolMapFilePath(mapFilePath),
            NodeId = nodeId,
            Prompt = prompt.Trim(),
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        });

        var entry = new LlmJobEntry(GetJobFilePath(job.Id), job);
        Save(entry);
        return entry;
    }

    public IReadOnlyList<LlmJobEntry> ListJobs()
    {
        if (!Directory.Exists(JobsDirectory))
            return Array.Empty<LlmJobEntry>();

        return Directory
            .EnumerateFiles(JobsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(TryLoadJob)
            .OfType<LlmJobEntry>()
            .OrderBy(entry => entry.Job.CreatedAt)
            .ThenBy(entry => entry.Job.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public LlmJobEntry? FindById(string jobId) =>
        ListJobs().FirstOrDefault(entry =>
            string.Equals(entry.Job.Id, jobId, StringComparison.OrdinalIgnoreCase));

    public LlmJobEntry? FindOpenByNode(string mapFilePath, Guid nodeId)
    {
        var protocolPath = BuildProtocolMapFilePath(mapFilePath);
        return ListJobs().FirstOrDefault(entry =>
            entry.Job.NodeId == nodeId &&
            IsOpen(entry.Job.Status) &&
            string.Equals(
                NormalizeProtocolPath(entry.Job.MapFilePath),
                NormalizeProtocolPath(protocolPath),
                StringComparison.OrdinalIgnoreCase));
    }

    public LlmJobEntry? FindOldestPending(string? jobId = null)
    {
        var jobs = ListJobs().Where(entry => entry.Job.Status == LlmJobStatus.Pending);
        if (!string.IsNullOrWhiteSpace(jobId))
        {
            return jobs.FirstOrDefault(entry =>
                string.Equals(entry.Job.Id, jobId, StringComparison.OrdinalIgnoreCase));
        }

        return jobs.FirstOrDefault();
    }

    public LlmJobEntry Claim(LlmJobEntry entry, string agentName)
    {
        var timestamp = DateTimeOffset.UtcNow;
        entry.Job.Status = LlmJobStatus.Claimed;
        entry.Job.ClaimedBy = string.IsNullOrWhiteSpace(agentName)
            ? LlmPromptService.DefaultAgentName
            : agentName.Trim();
        entry.Job.ClaimedAt = timestamp;
        entry.Job.UpdatedAt = timestamp;
        Save(entry);
        return entry;
    }

    public LlmJobEntry Complete(LlmJobEntry entry, Guid answerNodeId, string agentName)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var normalizedAgentName = string.IsNullOrWhiteSpace(agentName)
            ? LlmPromptService.DefaultAgentName
            : agentName.Trim();
        entry.Job.Status = LlmJobStatus.Completed;
        entry.Job.CompletedAt = timestamp;
        entry.Job.UpdatedAt = timestamp;
        entry.Job.ErrorMessage = null;
        entry.Job.Result = new LlmJobResult
        {
            MapFilePath = entry.Job.MapFilePath,
            PromptNodeId = entry.Job.NodeId,
            AnswerNodeId = answerNodeId,
            CompletedBy = normalizedAgentName,
            CompletedAt = timestamp
        };
        Save(entry);
        return entry;
    }

    public LlmJobEntry Fail(LlmJobEntry entry, string message)
    {
        var timestamp = DateTimeOffset.UtcNow;
        entry.Job.Status = LlmJobStatus.Failed;
        entry.Job.FailedAt = timestamp;
        entry.Job.UpdatedAt = timestamp;
        entry.Job.ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "The agent did not provide a failure message."
            : message.Trim();
        Save(entry);
        return entry;
    }

    public void Save(LlmJobEntry entry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(entry.FilePath)!);
        File.WriteAllText(
            entry.FilePath,
            JsonConvert.SerializeObject(Normalize(entry.Job), JsonSerialization.CreateDefaultSettings()));
    }

    public string ResolveMapPath(string mapFilePath)
    {
        if (string.IsNullOrWhiteSpace(mapFilePath))
            throw new InvalidOperationException("LLM job has no map file path.");

        if (Path.IsPathFullyQualified(mapFilePath) && File.Exists(mapFilePath))
            return Path.GetFullPath(mapFilePath);

        var normalized = NormalizeProtocolPath(mapFilePath);
        var directPath = Path.GetFullPath(Path.Combine(_mapsDirectory, normalized));
        if (File.Exists(directPath))
            return directPath;

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mapsDirectoryName = Path.GetFileName(_mapsDirectory);
        if (parts.Length > 1 &&
            string.Equals(parts[0], mapsDirectoryName, StringComparison.OrdinalIgnoreCase))
        {
            var relativeToMaps = Path.GetFullPath(Path.Combine(_mapsDirectory, Path.Combine(parts.Skip(1).ToArray())));
            if (File.Exists(relativeToMaps))
                return relativeToMaps;
        }

        var byFileName = Path.GetFullPath(Path.Combine(_mapsDirectory, Path.GetFileName(normalized)));
        if (File.Exists(byFileName))
            return byFileName;

        throw new FileNotFoundException($"Map \"{mapFilePath}\" was not found under \"{_mapsDirectory}\".");
    }

    public string BuildProtocolMapFilePath(string mapFilePath)
    {
        var fullMapPath = Path.GetFullPath(mapFilePath);
        var fullMapsDirectory = _mapsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var directoryPrefix = fullMapsDirectory + Path.DirectorySeparatorChar;
        if (fullMapPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(fullMapsDirectory, fullMapPath);
            return NormalizeProtocolPath(Path.Combine(Path.GetFileName(fullMapsDirectory), relativePath));
        }

        return NormalizeProtocolPath(fullMapPath);
    }

    public static bool IsOpen(string? status) =>
        status == LlmJobStatus.Pending || status == LlmJobStatus.Claimed;

    private string GetJobFilePath(string jobId) =>
        Path.Combine(JobsDirectory, $"{jobId}.json");

    private LlmJobEntry? TryLoadJob(string filePath)
    {
        try
        {
            var job = JsonConvert.DeserializeObject<LlmJob>(
                File.ReadAllText(filePath),
                JsonSerialization.CreateDefaultSettings());
            if (job == null)
                return null;

            if (string.IsNullOrWhiteSpace(job.Id))
                job.Id = Path.GetFileNameWithoutExtension(filePath);

            return new LlmJobEntry(filePath, Normalize(job));
        }
        catch
        {
            return null;
        }
    }

    private static LlmJob Normalize(LlmJob job)
    {
        job.Version = job.Version <= 0 ? 1 : job.Version;
        job.Id = string.IsNullOrWhiteSpace(job.Id)
            ? Guid.NewGuid().ToString("D")
            : job.Id.Trim();
        job.Status = job.Status switch
        {
            LlmJobStatus.Pending or LlmJobStatus.Claimed or LlmJobStatus.Completed or LlmJobStatus.Failed => job.Status,
            _ => LlmJobStatus.Pending
        };
        job.Mode = job.Mode == LlmPromptService.ContextMode
            ? job.Mode
            : LlmPromptService.ContextMode;
        job.MapFilePath = NormalizeProtocolPath(job.MapFilePath);
        job.Prompt = (job.Prompt ?? string.Empty).Trim();
        if (job.CreatedAt == default)
            job.CreatedAt = DateTimeOffset.UtcNow;
        if (job.UpdatedAt == default)
            job.UpdatedAt = job.CreatedAt;
        return job;
    }

    private static string NormalizeProtocolPath(string value) =>
        (value ?? string.Empty).Replace('\\', '/').TrimStart('/');
}
