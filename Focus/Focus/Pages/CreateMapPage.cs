#nullable enable

using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages;

internal sealed class CreateMapPage : Page
{
    private readonly FocusAppContext _appContext;
    private readonly string _fileName;
    private readonly MindMap _mindMap;

    public CreateMapPage(FocusAppContext appContext, string fileName, MindMap mindMap, string? sourceMapFilePath = null)
    {
        _appContext = appContext;
        _fileName = MapFileHelper.SanitizeFileName(fileName.Trim().Trim('\0'));
        _mindMap = mindMap;
    }

    public override void Show()
    {
        var mindMapsDir = _appContext.MapRepository.UserMindMapsDirectory;
        string? filePathDetermined = null;

        new RequestRenameUntilFileNameIsAvailableDialog(
            mindMapsDir,
            _fileName,
            filePath =>
            {
                _appContext.MapRepository.SaveMap(filePath, _mindMap);
                filePathDetermined = filePath;
            }).Show();

        if (!string.IsNullOrWhiteSpace(filePathDetermined))
        {
            _appContext.Navigator.OpenEditMap(filePathDetermined);
        }
    }
}
