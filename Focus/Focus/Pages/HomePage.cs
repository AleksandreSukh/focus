﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Edit;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages
{
    internal class HomePage : PageWithExclusiveOptions
    {
        public const string OptionNew = "new";
        public const string OptionDel = "del";
        public const string OptionExit = "exit";
        public const string OptionRefresh = "ls";

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
                var ribbon = new string('-', ribbonLength);
                Console.WriteLine("\n{0}{1}{0}\n", ribbon, title);

                _filesToChooseFrom = new Dictionary<int, FileInfo>();
                var existingMaps = _mapsStorage.GetTop(10);
                for (var index = 0; index < existingMaps.Length; index++)
                {
                    var mapFile = existingMaps[index];
                    _filesToChooseFrom.Add(index + 1, mapFile);
                    Console.WriteLine($"{index + 1} - {mapFile.NameWithoutExtension()}");
                }

                var input = GetCommand(
                    Environment.NewLine +
                    (existingMaps.Length > 0 ? $"Choose file number to open.{Environment.NewLine}" : "") +
                    $"Type \"{OptionNew}\" and file name to create new file{Environment.NewLine}" +
                    (_filesToChooseFrom.Any()
                        ? $"Type \"{OptionDel}\" and file number or name to delete{Environment.NewLine}"
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
                                ShowDeleteFileDialog(file);
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

        private static void ShowDeleteFileDialog(FileInfo file)
        {
            if (new Confirmation($"Are you sure you want to delete: \"{file.Name}\"?").Confirmed())
                file.Delete();
        }

        private void ShowFileNotFoundError(string input)
        {
            Console.WriteLine($"File {input} wasn't found. try again");
            Show();
        }

        private FileInfo FindFile(string fileIdentifier)
        {

            var invalidCharacters = Path.GetInvalidFileNameChars();

            if (int.TryParse(fileIdentifier, out int fileNumber) &&
                _filesToChooseFrom.TryGetValue(fileNumber, out FileInfo file))
            {
                return file;
            }
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
            var optionsWhenFileExists = new[] { OptionNew, OptionDel, OptionRefresh, OptionExit };
            var optionsWhenNoFileExists = new[] { OptionNew, OptionRefresh, OptionExit };
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