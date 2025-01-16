using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Edit;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages
{
    internal class HomePage : PageWithExclusiveOptions
    {
        public const string OptionNew = "new";
        public const string OptionRen = "ren";
        public const string OptionDel = "del";
        public const string OptionExit = "exit";
        public const string OptionRefresh = "ls";

        private static readonly string[] _fileOptions = new[] { OptionRen, OptionDel };

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
                var ribbonLength = (ConsoleWrapper.WindowWidth - title.Length) / 2;
                var ribbon = new string('-', Math.Max(0,ribbonLength));
                Console.WriteLine("\n{0}{1}{0}\n", ribbon, title);

                _filesToChooseFrom = new Dictionary<int, FileInfo>();
                var existingMaps = _mapsStorage.GetTop(100); //TODO:e
                for (var index = 0; index < existingMaps.Length; index++)
                {
                    var fileNumber = index + 1;
                    var mapFile = existingMaps[index];
                    _filesToChooseFrom.Add(index + 1, mapFile);

                    Console.WriteLine($"{fileNumber} ({AccessibleKeyNumbering.GetStringFor(fileNumber)}) - {mapFile.NameWithoutExtension()}");
                }

                LoadLinksFromAllFiles();

                var input = GetCommand(
                    Environment.NewLine +
                    (existingMaps.Length > 0 ? $"Choose file number to open.{Environment.NewLine}" : "") +
                    $"Type \"{OptionNew}\" and file name to create new file{Environment.NewLine}" +
                    (_filesToChooseFrom.Any()
                        ? $"Type \"{OptionDel}\" and file number or name to delete{Environment.NewLine}" +
                        $"Type \"{OptionRen}\" and file number or name to rename{Environment.NewLine}"
                        : ""
                    ) +
                    $"Type \"{OptionRefresh}\" - to refresh{Environment.NewLine}" +
                    $"Type \"{OptionExit}\" - to exit{Environment.NewLine}");

                if (string.IsNullOrWhiteSpace(input.InputString))
                    continue;

                switch (input.FirstWord)
                {
                    case OptionExit:
                        _shouldExit = true;
                        break;
                    case OptionNew:
                        {
                            HandleCreateFileCommand(input);
                        }
                        break;
                    case OptionRen:
                        {
                            HandleRenameFileCommand(input);
                        }
                        break;
                    case OptionDel:
                        {
                            HandleDeleteFileCommand(input);
                            break;
                        }
                    default:
                        {
                            HandleOpenFileCommand(input);
                            break;
                        }
                }
            }
        }

        private void HandleCreateFileCommand(ConsoleInput input)
        {
            var mapName = input.Parameters;
            new CreateMapPage(_mapsStorage, mapName, new MindMap(mapName)).Show();
        }

        private void HandleOpenFileCommand(ConsoleInput input)
        {
            var fileIdentifier = input.FirstWord;
            var file = FindFile(fileIdentifier);
            if (file == null)
                ShowFileNotFoundError(fileIdentifier);
            else
                new EditMapPage(file.FullName, _mapsStorage).Show();
        }

        private void HandleDeleteFileCommand(ConsoleInput input)
        {
            var fileIdentifier = input.Parameters;
            var file = FindFile(fileIdentifier);
            if (file == null)
                ShowFileNotFoundError(fileIdentifier);
            else
                ShowDeleteFileDialog(file);
        }

        private void HandleRenameFileCommand(ConsoleInput input)
        {
            var fileIdentifier = input.Parameters;
            var file = FindFile(fileIdentifier);
            if (file == null)
                ShowFileNotFoundError(fileIdentifier);
            else
                new RenameFileDialog(file).Show();
        }

        private void LoadLinksFromAllFiles()
        {
            //TODO: loading files just to fill the links (think of more elegant solution)
            if (!GlobalLinkDitionary.LinksLoaded)
            {
                foreach (var file in _filesToChooseFrom)
                {
                    MapFile.LoadLinks(file.Value.FullName);
                }
                GlobalLinkDitionary.LinksLoaded = true;
            }
        }

        private static void ShowDeleteFileDialog(FileInfo file)
        {
            if (new Confirmation($"Are you sure you want to delete: \"{file.Name}\"?").Confirmed())
                file.Delete();
        }

        private void ShowFileNotFoundError(string fileIdentifier)
        {
            Console.WriteLine($"File \"{fileIdentifier}\" wasn't found. try again");
            Show();
        }

        private FileInfo FindFile(string fileIdentifier)
        {
            //Option 1: Check if user input is file number
            if (int.TryParse(fileIdentifier, out int fileNumber) &&
                _filesToChooseFrom.TryGetValue(fileNumber, out FileInfo file))
            {
                return file;
            }

            //Option 2: Check if user input is file shortcut string
            if (fileIdentifier.Length <= AccessibleKeyNumbering.MaxShortcutStringLength)
            {
                var fileNumberFromShortcut = AccessibleKeyNumbering.GetNumberFor(fileIdentifier);
                if (fileNumberFromShortcut != 0 && _filesToChooseFrom.TryGetValue(fileNumber, out FileInfo fileFromShortcut))
                {
                    return fileFromShortcut;
                }
            }

            //Option 2: Check if user input is file name
            var fileNameIsValid = fileIdentifier.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

            if (fileNameIsValid)
            {
                if ((file = new FileInfo(Path.Combine(_mapsStorage.UserMindMapsDirectory,
                        fileIdentifier))).Exists
                    || (file = new FileInfo(Path.Combine(_mapsStorage.UserMindMapsDirectory,
                        $"{fileIdentifier}.json"))).Exists)
                {
                    return file;
                }
            }

            return null;
        }

        protected override string[] GetCommandOptions()
        {
            var optionsWhenFileExists = new[] { OptionNew, OptionRen, OptionDel, OptionRefresh, OptionExit };
            var optionsWhenNoFileExists = new[] { OptionNew, OptionRefresh, OptionExit };
            return _filesToChooseFrom.Any()
                ? _filesToChooseFrom.Keys.Select(k => k.ToString())
                    .Union(_filesToChooseFrom.Keys.Select(k => AccessibleKeyNumbering.GetStringFor(k)))
                    .Union(optionsWhenFileExists)
                    .ToArray()
                : optionsWhenNoFileExists;
        }

        //TODO
        protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index) =>
            !_filesToChooseFrom.Any()
                ? GetCommandOptions()
                : GetCommandOptions()
                    .Union(_fileOptions.SelectMany(opt => _filesToChooseFrom.Keys.Select(k => $"{opt} {k}")))
                    .Union(_fileOptions.SelectMany(opt => _filesToChooseFrom.Values.Select(k => $"{opt} {k}")))
                    .Union(_fileOptions.SelectMany(opt => _filesToChooseFrom.Keys.Select(k => AccessibleKeyNumbering.GetStringFor(k))));
    }
}