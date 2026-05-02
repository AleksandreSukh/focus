#nullable enable

namespace Systems.Sanity.Focus.Application.HomeCommands;

internal sealed class HomeCommandContext
{
    public HomeCommandContext(FocusAppContext appContext)
    {
        AppContext = appContext;
    }

    public FocusAppContext AppContext { get; }
}
