using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            var input = GetInput(prompt);
            ParseOptions(input.FirstWord);
            return input;
        }

        //TODO:Merge get options and parse options 
        private void ParseOptions(string input)
        {
            var options = GetCommandOptions().WithLocalizations().ToList();
            if (!string.IsNullOrWhiteSpace(input) && !options.Contains(input))
            {
                var messageBuilder = BuildInputErrorMessageDialogText(options);
                ColorfulConsole.WriteLine(messageBuilder.ToString()); 
                Console.ReadKey();

                Show();
            }
        }

        private static StringBuilder BuildInputErrorMessageDialogText(IEnumerable<string> options)
        {
            var messageBuilder = new StringBuilder();

            messageBuilder.AppendLine();
            messageBuilder.AppendLineCentered("*** Wrong Input ***");
            messageBuilder.AppendLine();
            messageBuilder.AppendLineCentered($"Valid options are:[{ConsoleColor.Green}]{string.Join(", ", options)}[!]");
            messageBuilder.AppendLine();
            messageBuilder.AppendLineCentered("Press any key to continue");
            messageBuilder.AppendLine();
            return messageBuilder;
        }
    }
}