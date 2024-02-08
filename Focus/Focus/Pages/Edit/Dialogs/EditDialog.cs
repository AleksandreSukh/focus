using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs
{
	internal class EditDialog : Page
	{
		private readonly MindMap _map;
		private readonly string _parameters;

		public EditDialog(MindMap map, string parameters)
		{
			_map = map;
			_parameters = parameters;
		}

		public override void Show()
		{
			var input = GetInput("Enter new text here: ", _map.GetCurrentNodeName()).InputString;
			_map.EditCurrentNode(input);
		}
	}
}