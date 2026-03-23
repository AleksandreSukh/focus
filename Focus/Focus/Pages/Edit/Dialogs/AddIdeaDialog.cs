using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs
{
    internal class AddIdeaDialog : Page
    {
        public bool DidAddIdeas { get; private set; }

        private readonly MindMap _map;

        public AddIdeaDialog(MindMap map)
        {
            _map = map;
        }

        public override void Show()
        {
            string input;
            while (!string.IsNullOrWhiteSpace(input = GetInput().InputString))
            {
                _map.AddIdeaAtCurrentNode(input);
                DidAddIdeas = true;
            }
        }

        public void ShowWithInitialInput(string input)
        {
            do
            {
                _map.AddIdeaAtCurrentNode(input);
                DidAddIdeas = true;
            }
            while (!string.IsNullOrWhiteSpace(input = GetInput().InputString));
        }
    }
}
