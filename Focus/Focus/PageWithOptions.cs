using System;
using System.Collections.Generic;
using System.Linq;

namespace Systems.Sanity.Focus
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
            var options = GetCommandOptions().WithLocalizations();
            if (!string.IsNullOrWhiteSpace(input) && !options.Contains(input))
            {
                Notify("Wrong!  - valid options are:" + string.Join(", ", options));
                Show();
            }
        }
    }
}