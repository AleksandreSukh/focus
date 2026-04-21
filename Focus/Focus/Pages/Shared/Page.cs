using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Application;
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
            AppConsole.Current.CommandLineEditor.SetAutoCompletionHandler(this);
            AppConsole.Current.CommandLineEditor.HistoryEnabled = true;
        }

        public abstract void Show();

        protected ConsoleInput GetInput(
            string prompt = "",
            string defaultInput = null,
            ConsoleKeyInfo? initialKeyInfo = null,
            Func<ConsoleKeyInfo, string, bool>? previewKeyHandler = null)
        {
            ColorfulConsole.WriteLine(prompt);
            prompt = string.Empty;
            if (defaultInput != null)
            {
                return new(AppConsole.Current.CommandLineEditor
                    .Read(
                        prompt,
                        defaultInput,
                        BeforeEachAutoComplete,
                        AfterEachAutoComplete,
                        previewKeyHandler,
                        initialKeyInfo: initialKeyInfo)
                    .Trim());
            }

            return new(AppConsole.Current.CommandLineEditor
                .Read(
                    prompt,
                    "",
                    BeforeEachAutoComplete,
                    AfterEachAutoComplete,
                    previewKeyHandler,
                    initialKeyInfo: initialKeyInfo)
                .Trim());
        }

        protected bool ProcessCommandInvariant(Func<string, bool> action, string parameters) => action(parameters)
            || CommandLanguageExtensions.IsOtherLanguage(parameters) && action(parameters.ToCommandLanguage());
    }
}
