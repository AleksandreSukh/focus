using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs
{
	internal class EditDialog : TextEditPage
    {
		private readonly MindMap _map;

		public EditDialog(MindMap map, string parameters) //TODO: remove unused param
		{
			_map = map;
		}

		public override void Show()
		{
			var input = GetInput("Enter new text here: ", _map.GetCurrentNodeName()).InputString;
			_map.EditCurrentNode(input);
		}
	}
}