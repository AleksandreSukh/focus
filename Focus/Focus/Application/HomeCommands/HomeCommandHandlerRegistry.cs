#nullable enable

namespace Systems.Sanity.Focus.Application.HomeCommands;

internal static class HomeCommandHandlerRegistry
{
    public static HomeCommandDispatcher CreateDefault() =>
        new(new IHomeCommandFeatureHandler[]
        {
            new HomeSystemCommandHandler(),
            new HomeFileCommandHandler(),
            new HomeFindCommandHandler()
        });
}
