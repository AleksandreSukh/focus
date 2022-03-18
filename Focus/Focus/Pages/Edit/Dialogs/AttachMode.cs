using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs
{
    //TODO: extract file loader functionality from here and HomePage to new infrastructrue class
    internal class AttachMode : PageWithSuggestedOptions
    {
        public const string OptionExit = "exit";

        private readonly MindMap _map;
        private readonly MapsStorage _mapsStorage;
        private Dictionary<int, FileInfo> _filesToChooseFrom;
        private bool _shouldExit;

        public AttachMode(MindMap map, MapsStorage mapsStorage)
        {
            _map = map;
            _mapsStorage = mapsStorage;
        }

        public override void Show()
        {
            while (!_shouldExit)
            {
                _filesToChooseFrom = new Dictionary<int, FileInfo>();
                var existingMaps = _mapsStorage.GetTop(10);
                for (var index = 0; index < existingMaps.Length; index++)
                {
                    var mapFile = existingMaps[index];
                    _filesToChooseFrom.Add(index + 1, mapFile);
                    Console.WriteLine($"{index + 1} - {mapFile.Name}");
                }

                var input = GetCommand(
                    Environment.NewLine +
                    $"Choose file number to open.{Environment.NewLine}" +
                    $"Type \"{OptionExit}\" - to exit{Environment.NewLine}");

                switch (input.FirstWord)
                {
                    case OptionExit:
                        SetToExit();
                        break;
                    default:
                        {
                            var fileIdentifier = input.FirstWord;
                            var file = FindFile(fileIdentifier);
                            if (file == null)
                                ShowFileNotFoundError(fileIdentifier);
                            else
                            {
                                LoadFile(file);
                                file.Delete();
                                SetToExit();
                            }
                            break;
                        }
                }
            }
        }

        private void SetToExit()
        {
            _shouldExit = true;
        }

        private void LoadFile(FileInfo file)
        {
            var map = MapFile.OpenFile(file.FullName);
            _map.LoadAtCurrentNode(map);
        }

        protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
        {
            return _filesToChooseFrom.Keys.Select(k => k.ToString());
        }

        private FileInfo FindFile(string fileIdentifier)
        {
            if (int.TryParse(fileIdentifier, out int fileNumber) &&
                _filesToChooseFrom.TryGetValue(fileNumber, out FileInfo file)
                || (file = new FileInfo(Path.Combine(_mapsStorage.UserMindMapsDirectory,
                    fileIdentifier))).Exists
                || (file = new FileInfo(Path.Combine(_mapsStorage.UserMindMapsDirectory,
                    $"{fileIdentifier}.json"))).Exists)
                return file;
            return null;
        }

        private void ShowFileNotFoundError(string input)
        {
            Console.WriteLine($"File {input} wasn't found. try again");
            Show();
        }
    }
}