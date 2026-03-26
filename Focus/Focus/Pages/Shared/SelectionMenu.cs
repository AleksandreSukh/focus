using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Shared
{
    internal class SelectionMenu : PageWithExclusiveOptions
    {
        private readonly Dictionary<string, int> _options;

        public SelectionMenu(IReadOnlyCollection<string> options)
        {
            _options = new Dictionary<string, int>();
            _options.Add("Cancel", 0);
            for (var i = 0; i < options.Count; i++)
            {
                _options.Add(options.ElementAt(i).ToLowerInvariant(), i + 1);
            }
        }

        public override void Show()
        {
            var messageBuilder = new StringBuilder();

            messageBuilder.AppendLine();
            messageBuilder.AppendLineCentered("*** Select Option ***");
            messageBuilder.AppendLine();
            for (var i = 0; i < _options.Count; i++)
            {
                messageBuilder.AppendLineCentered($"\"{_options.ElementAt(i).Value}\" or \"{_options.ElementAt(i).Key}\"");
            }
            messageBuilder.AppendLine();
            ColorfulConsole.WriteLine(messageBuilder.ToString());
        }

        public int GetSelectedOption()
        {
            var selectedOption = AppConsole.Current.CommandLineEditor.Read(string.Empty).ToLowerInvariant();
            if (int.TryParse(selectedOption, NumberStyles.Integer, null, out var optionNumber))
            {
                return optionNumber <= _options.Count ? optionNumber : 0;
            }

            if (!_options.ContainsKey(selectedOption))
            {
                return 0;
            }

            return _options[selectedOption];
        }

        protected override string[] GetCommandOptions()
        {
            return _options.Values.Select(v => v.ToString())
                .Union(_options.Keys)
                .ToArray();
        }
    }
}
