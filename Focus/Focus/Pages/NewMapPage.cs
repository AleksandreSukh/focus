using System;
using System.IO;
using System.Text.Json;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Pages.Edit;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages
{
    internal class NewMapPage : Page
    {
        private readonly MapsStorage _mapsStorage;
        private string _fileName;
        private readonly MindMap _mindMap;

        public NewMapPage(MapsStorage mapsStorage, string fileName, MindMap mindMap)
        {
            _mapsStorage = mapsStorage;
            _fileName = fileName;
            _mindMap = mindMap;
        }

        public override void Show()
        {
            var parentDir = new DirectoryInfo(_mapsStorage.UserMindMapsDirectory);
            if (!parentDir.Exists)
                parentDir.Create();

            var filePath = GetFullFilePath(_fileName);

            if (File.Exists(filePath))
            {
                var fileNameToAlter = Path.GetFileNameWithoutExtension(filePath);
                var suggestedFileName = $"{fileNameToAlter}_updated";

                var newName = ReadLine.Read($"File: {fileNameToAlter} already exists. Use suggested name {suggestedFileName} by pressing Enter or type new name below{Environment.NewLine}>");

                var newFileName = !string.IsNullOrWhiteSpace(newName)
                    ? newName
                    : suggestedFileName;

                filePath = GetFullFilePath(newFileName);
            }

            File.WriteAllText(filePath, JsonSerializer.Serialize(_mindMap));
            new EditMapPage(filePath, _mapsStorage).Show();
        }

        private string GetFullFilePath(string fileName)
        {
            if (!fileName.EndsWith(".json")) //TODO
                fileName += ".json";

            var filePath = Path.Combine(_mapsStorage.UserMindMapsDirectory, fileName);
            return filePath;
        }
    }
}