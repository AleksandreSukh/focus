using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
        public const string OptionUpdateApp = "update";

        private static readonly string[] FileOptions = { OptionRen, OptionDel };

        private readonly MapsStorage _mapsStorage;

        private Dictionary<int, FileInfo> _fileSelection;
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
                Console.WriteLine(GetHeaderRibbonString(title));

                _fileSelection = GetFileSelection();
                LoadLinksFromAllFiles(_fileSelection);

                var homePageText = GetHomePageText(_fileSelection);

                var input = GetCommand(homePageText);

                if (string.IsNullOrWhiteSpace(input.InputString))
                    continue;

                HandleInput(input);
            }
        }

        private static string GetHomePageText(Dictionary<int, FileInfo> files)
        {
            var sampleFileNumber = 1;
            var commandColor = ConfigurationConstants.CommandColor;
            var filesExist = files.Any();
            var homePageMenuTextBuilder = new StringBuilder();

            foreach (var f in files)
            {
                homePageMenuTextBuilder.AppendLine($"[{commandColor}]{AccessibleKeyNumbering.GetStringFor(f.Key)}[!]/[{commandColor}]{f.Key}[!] - {f.Value.NameWithoutExtension()}.");
                //homePageMenuTextBuilder.AppendLine($"{f.Key} ({AccessibleKeyNumbering.GetStringFor(f.Key)}) - {f.Value.NameWithoutExtension()}");
            }

            homePageMenuTextBuilder.AppendLine();

            if (filesExist)
            {
                homePageMenuTextBuilder.Append($"Type file identifier like \"[{commandColor}]{sampleFileNumber}[!]\" or \"[{commandColor}]{AccessibleKeyNumbering.GetStringFor(sampleFileNumber)}[!]\" to open file.{Environment.NewLine}");
            }

            homePageMenuTextBuilder.AppendLine(
                $"\"[{commandColor}]{OptionNew} and file name[!]\"\t - to create new file");

            if (filesExist)
            {
                homePageMenuTextBuilder.AppendLine($"\"[{commandColor}]{OptionDel} and identifier[!]\"\t - to delete");

                homePageMenuTextBuilder.AppendLine($"\"[{commandColor}]{OptionRen} and identifier[!]\"\t - to rename");
            }

            homePageMenuTextBuilder.AppendLine($"\"[{commandColor}]{OptionRefresh}[!]\" \t\t\t - to refresh list");
            homePageMenuTextBuilder.AppendLine($"\"[{commandColor}]{OptionExit}[!]\"\t\t\t - to exit app");

            var updatedVersion = AutoUpdateManager.CheckUpdatedVersion();

            if (updatedVersion != null)
            {
                homePageMenuTextBuilder.AppendLine($"\"[{commandColor}]{OptionUpdateApp}[!]\"\t - to update app to new version: \"{updatedVersion}\"");
            }

            var homePageText = homePageMenuTextBuilder.ToString();
            return homePageText;
        }


        private Dictionary<int, FileInfo> GetFileSelection()
        {
            var fileSelection = new Dictionary<int, FileInfo>();
            var existingMaps = _mapsStorage.GetTop(100);
            for (var index = 0; index < existingMaps.Length; index++)
            {
                var fileNumber = index + 1;
                var mapFile = existingMaps[index];
                fileSelection.Add(fileNumber, mapFile);
            }

            return fileSelection;
        }

        private static string GetHeaderRibbonString(string title)
        {
            var ribbonLength = (ConsoleWrapper.WindowWidth - title.Length) / 2;
            var ribbon = new string('-', Math.Max(0, ribbonLength));
            var headerRibbonString = string.Format("\n{0}{1}{0}\n", ribbon, title);
            return headerRibbonString;
        }

        private void HandleInput(ConsoleInput input)
        {
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
                case OptionUpdateApp:
                    {
                        HandleUpdateApp();
                        break;
                    }
                default:
                    {
                        HandleOpenFileCommand(input);
                        break;
                    }
            }
        }

        private void HandleUpdateApp()
        {
            AutoUpdateManager.HandleUpdate();
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

        private void LoadLinksFromAllFiles(Dictionary<int, FileInfo> filesToChooseFrom)
        {
            //TODO: loading files just to fill the links (think of more elegant solution)
            if (!GlobalLinkDitionary.LinksLoaded)
            {
                foreach (var file in filesToChooseFrom)
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
            if (FindFileByNumber(fileIdentifier, out var fileByNumber)) return fileByNumber;

            //Option 2: Check if user input is file shortcut string
            if (FindFileByShortcut(fileIdentifier, out var fileByShortcut)) return fileByShortcut;

            //Option 3: Check if user input is file name
            return FindByFileName(fileIdentifier, out var fileByName) ? fileByName : null;
        }

        private bool FindByFileName(string fileIdentifier, out FileInfo file)
        {
            var fileNameIsValid = fileIdentifier.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
            if (fileNameIsValid)
            {
                return (file = new FileInfo(Path.Combine(_mapsStorage.UserMindMapsDirectory,
                         fileIdentifier))).Exists
                     || (file = new FileInfo(Path.Combine(_mapsStorage.UserMindMapsDirectory,
                         $"{fileIdentifier}.json"))).Exists;
            }

            file = null;
            return false;

        }

        private bool FindFileByShortcut(string fileIdentifier, out FileInfo file)
        {
            var fileNumberFromShortcut = AccessibleKeyNumbering.GetNumberFor(fileIdentifier);
            if (fileNumberFromShortcut != 0 && _fileSelection.TryGetValue(fileNumberFromShortcut, out file))
            {
                return true;
            }

            file = null;
            return false;
        }

        private bool FindFileByNumber(string fileIdentifier, out FileInfo file)
        {
            if (int.TryParse(fileIdentifier, out int fileNumber) &&
                _fileSelection.TryGetValue(fileNumber, out file))
            {
                return true;
            }

            file = null;
            return false;
        }

        protected override string[] GetCommandOptions()
        {
            var optionsWhenFileExists = new[] { OptionNew, OptionRen, OptionDel, OptionRefresh, OptionExit, OptionUpdateApp };
            var optionsWhenNoFileExists = new[] { OptionNew, OptionRefresh, OptionExit, OptionUpdateApp };
            return _fileSelection.Any()
                ? _fileSelection.Keys.Select(k => k.ToString())
                    .Union(_fileSelection.Keys.Select(AccessibleKeyNumbering.GetStringFor))
                    .Union(optionsWhenFileExists)
                    .ToArray()
                : optionsWhenNoFileExists;
        }

        protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index) =>
            !_fileSelection.Any()
                ? GetCommandOptions()
                : GetCommandOptions()
                    .Union(FileOptions.SelectMany(opt => _fileSelection.Keys.Select(k => $"{opt} {k}")))
                    .Union(FileOptions.SelectMany(_ => _fileSelection.Keys.Select(AccessibleKeyNumbering.GetStringFor)));
    }
}