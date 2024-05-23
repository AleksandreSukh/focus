﻿using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Pages.Shared
{
    public abstract class Page : IAutoCompleteHandler
    {
        public string[] GetSuggestions(string text, int index)
        {
            return GetPageSpecificSuggestions(text, index)
                .ToArray();
        }

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
            if (defaultInput != null)
            {
                return new(ReadLine.Read(prompt, defaultInput).Trim());
            }

            return new(ReadLine.Read(prompt).Trim());
        }

        protected bool ProcessCommandInvariant(Func<string, bool> action, string parameters) => action(parameters)
            || CommandLanguageExtensions.IsOtherLanguage(parameters) && action(parameters.ToCommandLanguage());

    }
}