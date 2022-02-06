using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Systems.Sanity.Focus
{
    internal class HomePage : PageWithExclusiveOptions
    {
        public const string OptionNew = "new";
        public const string OptionDel = "del";
        public const string OptionExit = "exit";

        private readonly MapsStorage _mapsStorage;

        private Dictionary<int, FileInfo> _filesToChooseFrom;
        private bool _shouldExit;

        public HomePage(MapsStorage mapsStorage)
        {
            _mapsStorage = mapsStorage;
        }

        public override void Show()
        {
            while (!_shouldExit)
            {
                Console.Clear();
                var title = "Welcome";
                Console.Title = title;
                var ribbonLength = (Console.WindowWidth - title.Length) / 2;
                var ribbon = new string('-', ribbonLength);
                Console.WriteLine("\n{0}{1}{0}\n", ribbon, title);

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
                    (existingMaps.Length > 0 ? $"Choose file number to open.{Environment.NewLine}" : "") +
                    $"Type \"{OptionNew}\" and file name to create new file{Environment.NewLine}" +
                    (_filesToChooseFrom.Any()
                        ? $"Type \"{OptionDel}\" and file number or name to delete{Environment.NewLine}"
                        : ""
                    ) +
                    $"Type \"{OptionExit}\" - to exit{Environment.NewLine}");

                switch (input.FirstWord)
                {
                    case OptionExit:
                        _shouldExit = true;
                        break;
                    case OptionNew:
                        {
                            var mapName = input.Parameters;
                            new NewMapPage(_mapsStorage, mapName, new MindMap(mapName)).Show();
                        }
                        break;
                    case OptionDel:
                        {
                            var fileIdentifier = input.Parameters;
                            var file = FindFile(fileIdentifier);
                            if (file == null)
                                ShowFileNotFoundError(fileIdentifier);
                            else
                                new DeleteMapPage(file).Show();
                            break;
                        }
                    default:
                        {
                            var fileIdentifier = input.FirstWord;
                            var file = FindFile(fileIdentifier);
                            if (file == null)
                                ShowFileNotFoundError(fileIdentifier);
                            else
                                new EditMapPage(file.FullName, _mapsStorage).Show();
                            break;
                        }
                }
            }
        }

        private void ShowFileNotFoundError(string input)
        {
            Console.WriteLine($"File {input} wasn't found. try again");
            Show();
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

        protected override string[] GetCommandOptions()
        {
            var optionsWhenFileExists = new[] { OptionNew, OptionDel, OptionExit };
            var optionsWhenNoFileExists = new[] { OptionNew, OptionExit };
            return _filesToChooseFrom.Any()
                ? _filesToChooseFrom.Keys.Select(k => k.ToString())
                    .Union(optionsWhenFileExists)
                    .ToArray()
                : optionsWhenNoFileExists;
        }

        //TODO
        protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index) =>
            (!_filesToChooseFrom.Any()
                ? GetCommandOptions()
                : GetCommandOptions()
                    .Union(_filesToChooseFrom.Keys
                        .Select(k => $"{OptionDel} {k}"))
                    .Union(_filesToChooseFrom.Values
                        .Select(k => $"{OptionDel} {k.Name}"))
                    );
    }
}