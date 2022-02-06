using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const string GoToOptionSubOptionUp = "..";
        private const string RootOption = "ls";
        private const string DelOption = "del";
        private const string EditOption = "edit";
        private const string HideOption = "min";
        private const string UnhideOption = "max";
        private const string ExitOption = "exit";

        private readonly string[] _nodeOptions = new[] { GoToOption, DelOption, HideOption, UnhideOption, DetachOption };

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
            Redraw();

            while (!exit)
            {
                var commandResult = ProcessCommand();
                exit = commandResult.ShouldExit;
                if (commandResult.IsSuccess)
                {
                    Save();
                    Redraw();
                }
                else
                {
                    Redraw(commandResult.ErrorString);
                }
            }
            Save();
        }

        private CommandExecutionResult ProcessCommand()
        {
            var input = GetCommand();
            if (string.IsNullOrWhiteSpace(input.InputString))
                return CommandExecutionResult.Error("Empty command");
            var command = input.FirstWord.ToCommandLanguage();
            var parameters = input.Parameters;

            switch (command)
            {
                case ExitOption: return CommandExecutionResult.ExitCommand;
                case EditOption:
                    new EditMode(_map, parameters).Show();
                    return CommandExecutionResult.Success;
                case AddOption:
                    new AddMode(_map).Show();
                    return CommandExecutionResult.Success;
                case AttachOption:
                    new AttachMode(_map, _mapsStorage).Show();
                    return CommandExecutionResult.Success;
                case DetachOption: return ProcessDetach(parameters);
                case HideOption: return ProcessHide(parameters);
                case UnhideOption: return ProcessUnhide(parameters);
                case GoToOption: return ProcessGoTo(parameters);
                case DelOption: return ProcessCommandDel(parameters);
                case RootOption: return ProcessGoToRoot();
                case UpOption: return ProcessCommandGoUp();
                default:
                    {
                        if (ThereAreSubNodes())
                            return ProcessCommandGoToChild(command);

                        new AddMode(_map).ShowWithInitialInput(input.InputString);
                        return CommandExecutionResult.Success;
                    }
            }
        }

        private CommandExecutionResult ProcessGoToRoot()
        {
            if (_map.GoToRoot())
            {
                return CommandExecutionResult.Success;
            }
            return CommandExecutionResult.Error("Can't go to root");
        }

        private CommandExecutionResult ProcessGoTo(string parameters)
        {
            return parameters == GoToOptionSubOptionUp
                ? ProcessCommandGoUp()
                : ProcessCommandGoToChild(parameters);
        }

        private CommandExecutionResult ProcessDetach(string parameters)
        {
            var nodeIdentifier = parameters;
            var detachedMap = _map.DetachNode(nodeIdentifier);
            if (detachedMap == null)
            {
                return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");
            }

            new NewMapPage(_mapsStorage, detachedMap.RootNode.Name, detachedMap).Show();
            return CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessHide(string parameters)
        {
            var nodeIdentifier = parameters;
            if (_map.HideNode(nodeIdentifier))
            {
                return CommandExecutionResult.Success;
            }
            return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");
        }

        private CommandExecutionResult ProcessUnhide(string parameters)
        {
            var nodeIdentifier = parameters;
            if (_map.UnhideNode(nodeIdentifier))
            {
                return CommandExecutionResult.Success;
            }
            return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");
        }

        private CommandExecutionResult ProcessCommandDel(string parameters)
        {
            var nodeIdentifier = parameters;
            if (_map.HasNode(nodeIdentifier))
            {
                if (new Confirmation($"Are you sure to delete \"{nodeIdentifier}\"").Confirmed())
                {
                    if (_map.DeleteNode(nodeIdentifier))
                    {
                        return CommandExecutionResult.Success;
                    }
                }
            }

            return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");
        }

        private CommandExecutionResult ProcessCommandGoToChild(string parameters)
        {
            var nodeIdentifier = parameters;
            if (_map.ChangeCurrentNode(nodeIdentifier))
            {
                return CommandExecutionResult.Success;
            }
            return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");
        }

        private CommandExecutionResult ProcessCommandGoUp()
        {
            if (_map.GoUp())
            {
                return CommandExecutionResult.Success;
            }
            return CommandExecutionResult.Error("Can't go up");
        }

        private bool ThereAreSubNodes() => _map.GetChildren().Any();

        private void Redraw(string message = null)
        {
            Console.Clear();
            Console.WriteLine(_map.GetCurrentNodeString());

            if (!string.IsNullOrEmpty(message)) 
                Console.WriteLine($":! {message}{Environment.NewLine}");

            Console.WriteLine($":> {string.Join("; ", GetCommandOptions())}");
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
                    .Union(_nodeOptions.SelectMany(opt => childNodes.Keys.Select(k => $"{opt} {k}")))
                    .Union(_nodeOptions.SelectMany(opt => childNodes.Values.Select(k => $"{opt} {k}")));
        }
    }
}