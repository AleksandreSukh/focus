namespace Systems.Sanity.Focus.E2E.Tests;

internal sealed class FocusAppLaunchOptions
{
    public string? ClipboardText { get; init; }

    public byte[]? ClipboardImageBytes { get; init; }

    public string? ClipboardErrorMessage { get; init; }

    public string? ClipboardExceptionMessage { get; init; }

    public bool EmitTitles { get; init; }
}
