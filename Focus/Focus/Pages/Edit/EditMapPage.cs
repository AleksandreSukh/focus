using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Git;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Edit
{
    internal class EditMapPage : PageWithSuggestedOptions
    {
        private const string AddOption = "add";
        private const string AddIdeaOption = "idea";
        private const string ClearIdeasOption = "clearideas";
        private const string AttachOption = "attach";
        private const string DetachOption = "detach";
        private const string LinkFromOption = "linkfrom";
        private const string LinkToOption = "linkto";
        private const string UpOption = "up";
        private const string GoToOption = "cd";
        private const string GoToOptionSubOptionUp = "..";
        private const string RootOption = "ls";
        private const string DelOption = "del";
        private const string EditOption = "edit";
        private const string HideOption = "min";
        private const string UnhideOption = "max";
        private const string ExitOption = "exit";

        private readonly string[] _nodeOptions = new[] { GoToOption, EditOption, DelOption, HideOption, UnhideOption, DetachOption, LinkFromOption, LinkToOption };

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

            return command switch
            {
                ExitOption => Exit,
                EditOption => ProcessEdit(parameters),
                AddOption => ProcessAdd(),
                AddIdeaOption => ProcessAddIdea(parameters),
                ClearIdeasOption => ProcessClearIdeas(parameters),
                AttachOption => ProcessAttach(),
                DetachOption => ProcessDetach(parameters),
                LinkFromOption => ProcessLinkFrom(parameters),
                LinkToOption => ProcessLinkTo(parameters),
                HideOption => ProcessHide(parameters),
                UnhideOption => ProcessUnhide(parameters),
                GoToOption => ProcessGoTo(parameters),
                DelOption => ProcessCommandDel(parameters),
                RootOption => ProcessGoToRoot(),
                UpOption => ProcessCommandGoUp(),
                _ => ThereAreSubNodes() ? ProcessCommandGoToChild(command) : ProcessAddCurrentInputString(input)
            };
        }

        private CommandExecutionResult ProcessAddCurrentInputString(ConsoleInput input)
        {
            new AddNoteDialog(_map).ShowWithInitialInput(input.InputString);
            return CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessEdit(string parameters)
        {
            new EditDialog(_map, parameters).Show();
            return CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessAdd()
        {
            new AddNoteDialog(_map).Show();
            return CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessAttach()
        {
            new AttachMode(_map, _mapsStorage).Show();
            return CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessAddIdea(string parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                new AddIdeaDialog(_map).ShowWithInitialInput(parameters);
            }
            else
            {
                new AddIdeaDialog(_map).Show();
            }

            return CommandExecutionResult.Success;
        }

        private CommandExecutionResult ProcessClearIdeas(string parameters)
        {
            var nodeIdentifier = parameters;

            if (!string.IsNullOrWhiteSpace(nodeIdentifier))
            {
                if (!_map.HasNode(nodeIdentifier))
                    return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");

                var nodeContentPreview = _map.GetNodeContentPeek(nodeIdentifier);

                if (!new Confirmation($"Clear idea tags for: \"{nodeIdentifier}\". \"{nodeContentPreview}\"?").Confirmed())
                    return CommandExecutionResult.Error("Cancelled!");

                return _map.DeleteNodeIdeaTags(nodeIdentifier)
                    ? CommandExecutionResult.Success
                    : CommandExecutionResult.Error($"Couldn't remove \"{nodeIdentifier}\"");
            }


            if (!new Confirmation("Clear idea tags for current node?").Confirmed())
                return CommandExecutionResult.Error("Cancelled!");

            return _map.DeleteCurrentNodeIdeaTags()
                ? CommandExecutionResult.Success
                : CommandExecutionResult.Error("Can't delete current node (report a bug)");

        }

        private static CommandExecutionResult Exit => CommandExecutionResult.ExitCommand;

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

        private CommandExecutionResult ProcessLinkTo(string parameters)
        {
            var nodeIdentifier = parameters;
            if (!_map.HasNode(nodeIdentifier))
                return CommandExecutionResult.Error($"Couldn't find node \"{nodeIdentifier}\"");

            var targetNodePeek = _map.GetNodeContentPeek(nodeIdentifier);

            var nodeToLinkFrom = GlobalLinkDitionary.NodesToBeLinked.Peek();
            if (!new Confirmation($"Link \"{nodeToLinkFrom.Name}\" to \"{targetNodePeek}\"?").Confirmed())
                return CommandExecutionResult.Error("Cancelled!");

            var linkResult = _map.LinkToNode(nodeIdentifier, nodeToLinkFrom, "Link Metadata");
            if (!linkResult)
                return CommandExecutionResult.Error($"Unexpected result while linking \"{nodeIdentifier}\"");

            return CommandExecutionResult.Success;
        }
        private CommandExecutionResult ProcessLinkFrom(string parameters)
        {
            var nodeIdentifier = parameters;
            _map.AddNodeToLinkStack(nodeIdentifier);

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

            if (!string.IsNullOrWhiteSpace(nodeIdentifier))
            {
                if (!_map.HasNode(nodeIdentifier))
                    return CommandExecutionResult.Error($"Can't find \"{nodeIdentifier}\"");

                var nodeContentPreview = _map.GetNodeContentPeek(nodeIdentifier);

                if (!new Confirmation($"Are you sure to delete \"{nodeIdentifier}\". \"{nodeContentPreview}\"").Confirmed())
                    return CommandExecutionResult.Error("Cancelled!");

                return _map.DeleteNode(nodeIdentifier)
                    ? CommandExecutionResult.Success
                    : CommandExecutionResult.Error($"Couldn't remove \"{nodeIdentifier}\"");
            }

            if (_map.IsAtRootNode())
                return CommandExecutionResult.Error("Can't remove root node");

            if (!new Confirmation("Are you sure to delete current node").Confirmed())
                return CommandExecutionResult.Error("Cancelled!");

            return _map.DeleteCurrentNode()
                ? CommandExecutionResult.Success
                : CommandExecutionResult.Error("Can't delete current node (report a bug)");
        }

        private CommandExecutionResult ProcessCommandGoToChild(string parameters)
        {
            var nodeIdentifier = parameters;

            if (ProcessCommandInvariant(_map.ChangeCurrentNode, nodeIdentifier))
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
            return Exit;
        }

        private bool ThereAreSubNodes() => _map.GetChildren().Any();

        private void Redraw(string message = null)
        {
            try
            {
                Console.Clear();
            }
            catch (IOException e)
            {
                Console.Beep();
            }
            ColorfulConsole.WriteLine(_map.GetCurrentSubtreeString());

            if (!string.IsNullOrEmpty(message))
                ColorfulConsole.WriteLine($":! {message}{Environment.NewLine}");

            ColorfulConsole.WriteLine($":> {string.Join("; ", GetCommandOptions())}");
            if (GlobalLinkDitionary.NodesToBeLinked.Any())
            {
                ColorfulConsole.WriteLine($":Nodes to be linked> {string.Join("; ", GlobalLinkDitionary.NodesToBeLinked.Select(n => n.Name))}");
            }
        }

        private void Save()
        {
            _map.SaveTo(_filePath);
            _gitHelper?.SyncronizeToRemote();
        }

        private string[] GetCommandOptions() =>
            new[] { AddOption, AddIdeaOption, ClearIdeasOption, AttachOption, DetachOption, LinkFromOption, LinkToOption, HideOption, UnhideOption, ExitOption, GoToOption, EditOption, DelOption, UpOption, RootOption }
                .Union(_map.GetChildren().Keys.Select(k => k.ToString()))
                .Union(_map.GetChildren().Keys.Select(k => AccessibleKeyNumbering.GetStringFor(k)))
                .ToArray();

        protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
        {
            var childNodes = _map.GetChildren();
            if (!childNodes.Any())
                return GetCommandOptions();

            //TODO: Use similar approach in homePage
            return GetCommandOptions()
                    .Union(childNodes.Keys.Select(k => k.ToString()))
                    .Union(_nodeOptions.SelectMany(opt => childNodes.Keys.Select(k => $"{opt} {k}")))
                    .Union(_nodeOptions.SelectMany(opt => childNodes.Values.Select(k => $"{opt} {k}")));
        }
    }
}