#nullable enable

using System;

namespace Systems.Sanity.Focus.Application.Llm;

internal static class LlmJobStatus
{
    public const string Pending = "pending";
    public const string Claimed = "claimed";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

internal sealed class LlmJob
{
    public int Version { get; set; } = 1;

    public string Id { get; set; } = string.Empty;

    public string Status { get; set; } = LlmJobStatus.Pending;

    public string Mode { get; set; } = LlmPromptService.ContextMode;

    public string MapFilePath { get; set; } = string.Empty;

    public Guid NodeId { get; set; }

    public string Prompt { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? ClaimedBy { get; set; }

    public DateTimeOffset? ClaimedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? FailedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public LlmJobResult? Result { get; set; }
}

internal sealed class LlmJobResult
{
    public string MapFilePath { get; set; } = string.Empty;

    public Guid PromptNodeId { get; set; }

    public Guid AnswerNodeId { get; set; }

    public string CompletedBy { get; set; } = string.Empty;

    public DateTimeOffset CompletedAt { get; set; }
}

internal sealed record LlmJobEntry(string FilePath, LlmJob Job);
