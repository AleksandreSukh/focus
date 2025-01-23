using System;
using System.Text;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;

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
            messageBuilder.AppendLineCentered($"*** {_message} ***");
            messageBuilder.AppendLine();
            messageBuilder.AppendLineCentered($"Type \"{OptionYes}\" to confirm or \"Enter\" to cancel");
            messageBuilder.AppendLine();

            return ReadLine.Read(messageBuilder.ToString())
                .ToLowerInvariant() == OptionYes;
        }
    }
}