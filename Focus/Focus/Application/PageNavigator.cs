using System;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Pages;
using Systems.Sanity.Focus.Pages.Edit;

namespace Systems.Sanity.Focus.Application;

internal sealed class PageNavigator : IPageNavigator
{
    private readonly FocusAppContext _appContext;

    public PageNavigator(FocusAppContext appContext)
    {
        _appContext = appContext;
    }

    public void OpenCreateMap(string fileName, MindMap mindMap)
    {
        new CreateMapPage(_appContext, fileName, mindMap).Show();
    }

    public void OpenEditMap(string filePath, Guid? initialNodeIdentifier = null)
    {
        _appContext.StartupSyncNotificationState.SetCurrentOpenFile(filePath);
        try
        {
            new EditMapPage(filePath, _appContext, initialNodeIdentifier).Show();
        }
        finally
        {
            _appContext.StartupSyncNotificationState.ClearCurrentOpenFile(filePath);
        }
    }
}
