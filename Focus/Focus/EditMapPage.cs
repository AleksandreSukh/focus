﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Git;

namespace Systems.Sanity.Focus
{
    internal class EditMapPage : PageWithSuggestedOptions
    {
        private const string AddOption = "add";
        private const string AttachOption = "attach";
        private const string DetachOption = "detach";
        private const string UpOption = "up";
        private const string GoToOption = "cd";
        private const string GoToOptionSubOption_Up = "..";
        private const string RootOption = "ls";
        private const string DelOption = "del";
        private const string EditOption = "edit";
        private const string HideOption = "min";
        private const string UnhideOption = "max";
        private const string ExitOption = "exit";

        private readonly string[] NodeOptions = new[] { GoToOption, DelOption, HideOption, UnhideOption, DetachOption };

        private readonly string _filePath;
        private MindMap _map;
        private readonly MapsStorage _mapsStorage;
        private readonly GitHelper _gitHelper;

        public EditMapPage(
            string filePath,
            MapsStorage mapsStorage)
        {
            _filePath = filePath;
            _mapsStorage = mapsStorage;
            _map = MapFile.OpenFile(_filePath);
            var gitRepositoryName = _mapsStorage.GitRepository;
            if (!string.IsNullOrWhiteSpace(gitRepositoryName))
            {
                _gitHelper = new GitHelper(gitRepositoryName);
            }
        }

        public override void Show()
        {
            Console.Title = Path.GetFileName(_filePath) ?? Console.Title;
            var exit = false;
            while (!exit)
            {
                Save();
                Redraw();
                exit = ProcessCommand();
            }
            Console.WriteLine("Saving map");
            Save();
        }

        private bool ProcessCommand()
        {
            var input = GetCommand();
            var command = input.FirstWord.ToCommandLanguage();
            var parameters = input.Parameters;

            switch (command)
            {
                case ExitOption:
                    return true;
                case EditOption:
                    new EditMode(_map, parameters).Show();
                    break;
                case AddOption:
                    new AddMode(_map).Show();
                    break;
                case AttachOption:
                    new AttachMode(_map, _mapsStorage).Show();
                    break;
                case DetachOption:
                    {
                        ProcessDetach(parameters);
                        break;
                    }
                case HideOption:
                    {
                        ProcessHide(parameters);
                        break;
                    }
                case UnhideOption:
                    {
                        ProcessUnhide(parameters);
                        break;
                    }
                case GoToOption:
                    {
                        ProcessGoTo(parameters);
                        break;
                    }
                case DelOption:
                    {
                        ProcessCommandDel(parameters);
                        break;
                    }
                case RootOption:
                    ProcessGoToRoot();
                    break;
                case UpOption:
                    ProcessCommandGoUp();
                    break;
                default:
                    {
                        if (ThereAreSubNodes())
                        {
                            ProcessCommandGoToChild(command);
                        }
                        else
                        {
                            new AddMode(_map).ShowWithInitialInput(input.InputString);
                        }
                    }
                    break;
            }

            return false;
        }

        private void ProcessGoToRoot()
        {
            if (_map.GoToRoot())
                Redraw();
            else Console.WriteLine("Can't go to root");
        }

        private void ProcessGoTo(string parameters)
        {
            if (parameters == GoToOptionSubOption_Up)
            {
                ProcessCommandGoUp();
                return;
            }

            ProcessCommandGoToChild(parameters);
        }

        private void ProcessDetach(string parameters)
        {
            var nodeIdentifier = parameters;
            var detachedMap = _map.DetachNode(nodeIdentifier);
            if (detachedMap == null)
            {
                Console.WriteLine($"Can't find \"{nodeIdentifier}\"");
                return;
            }

            new NewMapPage(_mapsStorage, detachedMap.RootNode.Name, detachedMap).Show();
        }

        private void ProcessHide(string parameters)
        {
            var nodeIdentifier = parameters;
            if (_map.HideNode(nodeIdentifier))
                Redraw();
            else Console.WriteLine($"Can't find \"{nodeIdentifier}\"");
        }

        private void ProcessUnhide(string parameters)
        {
            var nodeIdentifier = parameters;
            if (_map.UnhideNode(nodeIdentifier))
                Redraw();
            else Console.WriteLine($"Can't find \"{nodeIdentifier}\"");
        }

        private void ProcessCommandDel(string parameters)
        {
            var nodeIdentifier = parameters;
            if (!_map.HasNode(nodeIdentifier))
            {
                Console.WriteLine($"Can't find \"{nodeIdentifier}\"");
                return;
            }

            if (new Confirmation($"Are you sure to delete \"{nodeIdentifier}\"").Confirmed())
            {
                if (_map.DeleteNode(nodeIdentifier))
                    Redraw();
            }
        }

        private void ProcessCommandGoToChild(string parameters)
        {
            var nodeIdentifier = parameters;
            if (_map.ChangeCurrentNode(nodeIdentifier))
                Redraw();
            else Console.WriteLine($"Can't find \"{nodeIdentifier}\"");
        }

        private void ProcessCommandGoUp()
        {
            if (_map.GoUp())
                Redraw();
            else Console.WriteLine("Can't go up");
        }

        private bool ThereAreSubNodes() => _map.GetChildren().Any();

        private void Redraw()
        {
            Console.Clear();
            Console.WriteLine(_map.GetCurrentNodeString());
            Console.WriteLine($">:{string.Join("; ", GetCommandOptions())}");
        }

        private void Save()
        {
            _map.SaveTo(_filePath);
            _gitHelper?.SyncronizeToRemote();
        }

        private string[] GetCommandOptions() =>
            new[] { AddOption, AttachOption, DetachOption, HideOption, UnhideOption, ExitOption, GoToOption, EditOption, DelOption, UpOption, RootOption }
                .Union(_map.GetChildren().Keys.Select(k => k.ToString()))
                .Union(_map.GetChildren().Keys.Select(k => AccessibleKeyNumbering.GetStringFor(k)))
                .ToArray();

        protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
        {
            var childNodes = _map.GetChildren();
            if (!childNodes.Any())
                return GetCommandOptions();

            return GetCommandOptions()
                    .Union(childNodes.Keys.Select(k => k.ToString()))
                    .Union(NodeOptions.SelectMany(opt => childNodes.Keys.Select(k => $"{opt} {k}")))
                    .Union(NodeOptions.SelectMany(opt => childNodes.Values.Select(k => $"{opt} {k}")));
        }
    }
}