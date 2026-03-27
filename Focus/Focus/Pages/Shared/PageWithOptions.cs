using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Shared
{
    public abstract class PageWithExclusiveOptions : Page
    {
        protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
        {
            return GetCommandOptions();
        }

        protected abstract string[] GetCommandOptions();

        protected ConsoleInput GetCommand(string prompt = "")
        {
            while (true)
            {
                var input = GetInput(prompt);
                if (IsValidOption(input.FirstWord))
                    return input;

                ColorfulConsole.WriteLine(BuildInputErrorMessageDialogText(GetCommandOptions().WithLocalizations()));
                AppConsole.Current.ReadKey();
            }
        }

        private bool IsValidOption(string input)
        {
            var options = GetCommandOptions().WithLocalizations().ToList();
            return string.IsNullOrWhiteSpace(input) || options.Contains(input);
        }

        internal static string BuildInputErrorMessageDialogText(IEnumerable<string> options)
        {
            var messageBuilder = new StringBuilder();

            messageBuilder.AppendLine();
            messageBuilder.AppendLineCentered("*** Wrong Input ***");
            messageBuilder.AppendLine();
            messageBuilder.Append(CommandHelpFormatter.BuildWrappedOptionList("Valid options are", options));
            messageBuilder.AppendLine();
            messageBuilder.AppendLineCentered("Press any key to continue");
            messageBuilder.AppendLine();
            return messageBuilder.ToString();
        }
    }
}
