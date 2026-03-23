using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs
{
	internal class EditDialog : TextEditPage
    {
		public bool DidEdit { get; private set; }

		private readonly MindMap _map;

		public EditDialog(MindMap map, string parameters) //TODO: remove unused param
		{
			_map = map;
		}

		public override void Show()
		{
			var currentNodeName = _map.GetCurrentNodeName();
			var input = GetInput("Enter new text here: ", currentNodeName).InputString;

			if (input != currentNodeName)
			{
				_map.EditCurrentNode(input);
				DidEdit = true;
			}
		}
	}
}
