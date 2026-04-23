using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal sealed class EditBlockDialog : BlockTextEditPage
{
    private const string Prompt =
        "Enter new block text here. Press Enter twice to finish. Press Enter twice immediately to keep current text:";

    private readonly MindMap _map;

    public EditBlockDialog(MindMap map)
    {
        _map = map;
    }

    public bool DidEdit { get; private set; }

    public override void Show()
    {
        var currentNodeName = _map.GetCurrentNodeName();
        var input = GetMultilineInput(Prompt, currentNodeName);

        if (input == currentNodeName)
            return;

        _map.EditCurrentNode(input);
        DidEdit = true;
    }
}
