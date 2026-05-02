using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Tests;

public class CommandHelpVisibilityStateTests
{
    [Fact]
    public void ShowCommands_DefaultsToFalse()
    {
        var state = new CommandHelpVisibilityState();

        Assert.False(state.ShowCommands);
    }

    [Fact]
    public void TryHandlePreviewKey_TildeOnEmptyInput_TogglesVisibility()
    {
        var state = new CommandHelpVisibilityState();

        Assert.True(state.TryHandlePreviewKey(CommandHelpVisibilityState.BuildToggleKeyInfo(), string.Empty));
        Assert.True(state.ShowCommands);

        Assert.True(state.TryHandlePreviewKey(CommandHelpVisibilityState.BuildToggleKeyInfo(), string.Empty));
        Assert.False(state.ShowCommands);
    }

    [Fact]
    public void TryHandlePreviewKey_TildeWithCurrentText_IsIgnored()
    {
        var state = new CommandHelpVisibilityState();

        Assert.False(state.TryHandlePreviewKey(CommandHelpVisibilityState.BuildToggleKeyInfo(), "ex"));

        Assert.False(state.ShowCommands);
    }
}
