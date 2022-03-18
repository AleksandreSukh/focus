using System;

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
            return ReadLine.Read($"{_message}{Environment.NewLine}Type {OptionYes} to confirm or return{Environment.NewLine}>")
                .ToLowerInvariant() == OptionYes;
        }
    }
}