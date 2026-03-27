using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Application;

internal sealed class FocusAppContext
{
    public FocusAppContext(MapsStorage mapsStorage)
    {
        MapsStorage = mapsStorage;
        StartupSyncNotificationState = new StartupSyncNotificationState();
        LinkIndex = new LinkIndex();
        LinkNavigationService = new LinkNavigationService(LinkIndex);
        MapSelectionService = new MapSelectionService(mapsStorage);
        Navigator = new PageNavigator(this);
    }

    public MapsStorage MapsStorage { get; }

    public IMapRepository MapRepository => MapsStorage;

    public ILinkIndex LinkIndex { get; }

    public LinkNavigationService LinkNavigationService { get; }

    public MapSelectionService MapSelectionService { get; }

    public IPageNavigator Navigator { get; }

    public StartupSyncNotificationState StartupSyncNotificationState { get; }

    public void RefreshLinkIndex()
    {
        LinkIndex.Rebuild(MapRepository);
    }
}
