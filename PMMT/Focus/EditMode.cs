namespace Systems.Sanity.Focus
{
    internal class EditMode : Page
    {
        private readonly MindMap _map;
        private readonly string _parameters;

        public EditMode(MindMap map, string parameters)
        {
            _map = map;
            _parameters = parameters;
        }

        public override void Show()
        {
            if (!string.IsNullOrWhiteSpace(_parameters))
                _map.EditCurrentNode(_parameters);
            else
            {
                var input = GetInput("Enter new text here: ").InputString;
                _map.EditCurrentNode(input);
            }
        }
    }
}