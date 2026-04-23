using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal sealed class AddBlockDialog : BlockTextEditPage
{
    private const string Prompt = "Enter block text here. Press Enter twice to finish:";

    private readonly MindMap _map;

    public AddBlockDialog(MindMap map)
    {
        _map = map;
    }

    public bool DidAddBlock { get; private set; }

    public override void Show()
    {
        var input = GetMultilineInput(Prompt);
        if (string.IsNullOrWhiteSpace(input))
            return;

        _map.AddBlockAtCurrentNode(input);
        DidAddBlock = true;
    }
}
