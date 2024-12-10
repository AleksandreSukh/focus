using System.Text;

namespace Systems.Sanity.Focus.Pages.Shared.Dialogs;

internal class Notification
{
    private readonly string _message;
    public Notification(string message)
    {
        _message = message;
    }

    public void Show()
    {
        var messageBuilder = new StringBuilder();

        messageBuilder.AppendLine();
        messageBuilder.AppendLineCentered($"*** {_message} ***");
        messageBuilder.AppendLine();
        messageBuilder.AppendLineCentered($"Press \"Enter\" to continue");
        messageBuilder.AppendLine();
    }
}