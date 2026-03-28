#nullable enable

using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Application;

internal sealed class FocusAppContext
{
    public FocusAppContext(MapsStorage mapsStorage)
        : this(mapsStorage, navigator: null)
    {
    }

    internal FocusAppContext(
        MapsStorage mapsStorage,
        IPageNavigator? navigator,
        IClipboardCaptureService? clipboardCaptureService = null,
        IFileOpener? fileOpener = null)
    {
        MapsStorage = mapsStorage;
        StartupSyncNotificationState = new StartupSyncNotificationState();
        LinkIndex = new LinkIndex();
        LinkNavigationService = new LinkNavigationService(LinkIndex);
        MapSelectionService = new MapSelectionService(mapsStorage);
        ClipboardCaptureService = clipboardCaptureService ?? ClipboardCaptureServiceFactory.CreateDefault();
        FileOpener = fileOpener ?? new DefaultFileOpener();
        Navigator = navigator ?? new PageNavigator(this);
    }

    public MapsStorage MapsStorage { get; }

    public IMapRepository MapRepository => MapsStorage;

    public ILinkIndex LinkIndex { get; }

    public LinkNavigationService LinkNavigationService { get; }

    public MapSelectionService MapSelectionService { get; }

    public IClipboardCaptureService ClipboardCaptureService { get; }

    public IFileOpener FileOpener { get; }

    public IPageNavigator Navigator { get; }

    public StartupSyncNotificationState StartupSyncNotificationState { get; }

    public void RefreshLinkIndex()
    {
        LinkIndex.Rebuild(MapRepository);
    }
}
