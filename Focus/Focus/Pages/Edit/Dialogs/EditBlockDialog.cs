using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal sealed class EditBlockDialog : BlockTextEditPage
{
    private const string Prompt =
        "Edit block text. Press Enter through prefilled lines, then Enter twice to finish. Clear all text to keep current text:";

    private readonly MindMap _map;

    public EditBlockDialog(MindMap map)
    {
        _map = map;
    }

    public bool DidEdit { get; private set; }

    public override void Show()
    {
        var currentNodeName = _map.GetCurrentNodeName();
        var input = GetMultilineInput(Prompt, currentNodeName, currentNodeName);

        if (input == currentNodeName)
            return;

        _map.EditCurrentNode(input);
        DidEdit = true;
    }
}
