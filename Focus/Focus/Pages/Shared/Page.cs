using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;

namespace Systems.Sanity.Focus.Pages.Shared
{
    public abstract class Page : IAutoCompleteHandler
    {
        public string[] GetSuggestions(string text, int index)
        {
            return GetPageSpecificSuggestions(text, index)
                .ToArray();
        }

        public virtual void BeforeEachAutoComplete(string str) { }

        public virtual void AfterEachAutoComplete(string str) { }

        public char[] Separators { get; set; } = new char[] { ' ' };

        protected virtual IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
            => Array.Empty<string>();

        protected Page()
        {
            ReadLine.AutoCompletionHandler = this;
            ReadLine.HistoryEnabled = true;
        }

        public abstract void Show();
        protected ConsoleInput GetInput(string prompt = "", string defaultInput = null)
        {
            ColorfulConsole.WriteLine(prompt);
            prompt = string.Empty;
            if (defaultInput != null)
            {
                return new(ReadLine.Read(prompt, defaultInput, BeforeEachAutoComplete, AfterEachAutoComplete).Trim());
            }

            return new(ReadLine.Read(prompt, "", BeforeEachAutoComplete, AfterEachAutoComplete).Trim());
        }

        protected bool ProcessCommandInvariant(Func<string, bool> action, string parameters) => action(parameters)
            || CommandLanguageExtensions.IsOtherLanguage(parameters) && action(parameters.ToCommandLanguage());

    }
}