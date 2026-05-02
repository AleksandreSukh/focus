using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Application;

internal enum ExportDestination
{
    File,
    ClipboardText
}

internal sealed record ExportRequest(
    ExportFormat Format,
    string FileName,
    bool SkipCollapsedDescendants,
    bool UseBlackBackground = false,
    bool IncludeAttachments = false,
    ExportDestination Destination = ExportDestination.File);
