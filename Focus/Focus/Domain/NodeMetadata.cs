#nullable enable

using System;
using System.Collections.Generic;

namespace Systems.Sanity.Focus.Domain;

public sealed class NodeMetadata
{
    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string? Source { get; set; }

    public string? Device { get; set; }

    public List<NodeAttachment> Attachments { get; set; } = new();

    public static NodeMetadata Create(
        string? source,
        string? device,
        DateTimeOffset? timestampUtc = null)
    {
        var normalizedTimestamp = NormalizeTimestamp(timestampUtc);
        return new NodeMetadata
        {
            CreatedAtUtc = normalizedTimestamp,
            UpdatedAtUtc = normalizedTimestamp,
            Source = source,
            Device = device,
            Attachments = new()
        };
    }

    public void Touch(DateTimeOffset? timestampUtc = null)
    {
        UpdatedAtUtc = NormalizeTimestamp(timestampUtc);
    }

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset? timestampUtc) =>
        (timestampUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
}

public sealed class NodeAttachment
{
    public Guid Id { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
