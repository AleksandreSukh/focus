using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Pages.Edit;

internal sealed record ExportRequest(
    ExportFormat Format,
    string FileName,
    bool SkipCollapsedDescendants,
    bool UseBlackBackground = false,
    bool IncludeAttachments = false);
