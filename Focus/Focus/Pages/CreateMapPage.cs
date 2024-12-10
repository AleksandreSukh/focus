using System.IO;
using System.Text.Json;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Pages.Edit;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages
{
    internal class CreateMapPage : Page
    {
        private readonly MapsStorage _mapsStorage;
        private string _fileName;
        private readonly MindMap _mindMap;

        public CreateMapPage(MapsStorage mapsStorage, string fileName, MindMap mindMap)
        {
            _mapsStorage = mapsStorage;
            _fileName = fileName;
            _mindMap = mindMap;
        }

        public override void Show()
        {
            var mindMapsDir = _mapsStorage.UserMindMapsDirectory;
            string filePathDetermied = null;
            new RequestRenameUntilFileNameIsAvailableDialog(mindMapsDir, _fileName, filePath =>
            {
                File.WriteAllText(filePath, JsonSerializer.Serialize(_mindMap));
                filePathDetermied = filePath;
            }).Show();
            new EditMapPage(filePathDetermied, _mapsStorage).Show();
        }
    }
}