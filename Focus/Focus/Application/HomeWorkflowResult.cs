#nullable enable

namespace Systems.Sanity.Focus.Application;

internal sealed record HomeWorkflowResult(
    bool ShouldExit,
    string? Message = null,
    bool IsError = false)
{
    public static readonly HomeWorkflowResult Continue = new(false);
    public static readonly HomeWorkflowResult Exit = new(true);

    public static HomeWorkflowResult Error(string message) => new(false, message, true);
}
