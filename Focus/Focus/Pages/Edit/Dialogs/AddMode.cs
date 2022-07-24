﻿using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs
{
    internal class AddMode : Page
    {
        private readonly MindMap _map;

        public AddMode(MindMap map)
        {
            _map = map;
        }

        public override void Show()
        {
            string input;
            while (!string.IsNullOrWhiteSpace(input = GetInput().InputString))
            {
                _map.AddAtCurrentNode(input);
            }
        }

        public void ShowWithInitialInput(string input)
        {
            do
            {
                _map.AddAtCurrentNode(input);
            } 
            while (!string.IsNullOrWhiteSpace(input = GetInput().InputString));
        }
    }    
    
    //TODO: almost duplicate of AddMode
    internal class AddIdeaMode : Page
    {
        private readonly MindMap _map;

        public AddIdeaMode(MindMap map)
        {
            _map = map;
        }

        public override void Show()
        {
            string input;
            while (!string.IsNullOrWhiteSpace(input = GetInput().InputString))
            {
                _map.AddIdeaAtCurrentNode(input);
            }
        }

        public void ShowWithInitialInput(string input)
        {
            do
            {
                _map.AddIdeaAtCurrentNode(input);
            } 
            while (!string.IsNullOrWhiteSpace(input = GetInput().InputString));
        }
    }
}