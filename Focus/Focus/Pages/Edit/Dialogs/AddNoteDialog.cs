using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs
{
    internal class AddNoteDialog : TextEditPage
    {
        private readonly MindMap _map;

        public AddNoteDialog(MindMap map)
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
}