using System;
using System.Text;
using Systems.Sanity.Focus.Application;

namespace Systems.Sanity.Focus.Pages.Shared.Dialogs
{
    internal class Confirmation
    {
        private readonly string _message;
        private const string OptionYes = "yes";

        public Confirmation(string message)
        {
            _message = message;
        }

        public bool Confirmed()
        {
            var messageBuilder = new StringBuilder();

            messageBuilder.AppendLine();
            AppendMessage(messageBuilder);
            messageBuilder.AppendLine();
            messageBuilder.AppendLineCentered($"Type \"{OptionYes}\" to confirm or \"Enter\" to cancel");
            messageBuilder.AppendLine();

            return AppConsole.Current.CommandLineEditor.Read(messageBuilder.ToString())
                .ToLowerInvariant() == OptionYes;
        }

        private void AppendMessage(StringBuilder messageBuilder)
        {
            var messageLines = _message.ReplaceLineEndings("\n").Split('\n');
            if (messageLines.Length == 1)
            {
                messageBuilder.AppendLineCentered($"*** {_message} ***");
                return;
            }

            messageBuilder.AppendLineCentered("***");
            foreach (var messageLine in messageLines)
            {
                messageBuilder.AppendLineCentered(messageLine);
            }

            messageBuilder.AppendLineCentered("***");
        }
    }
}
