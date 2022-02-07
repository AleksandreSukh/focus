using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus
{
    public abstract class Page : IAutoCompleteHandler
    {
        public string[] GetSuggestions(string text, int index)
        {
            return GetPageSpecificSuggestions(text, index)
                .Where(i => i.Length > text.Length && i.StartsWith(text))
                .ToArray();
        }

        public char[] Separators { get; set; } = new char[] { ' ' };

        protected virtual IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
            => Array.Empty<string>();

        protected Page()
        {
            ReadLine.AutoCompletionHandler = this;
        }

        public abstract void Show();
        protected ConsoleInput GetInput(string prompt = "") =>
            new(ReadLine.Read(prompt).Trim());

        protected void Notify(string notification)
        {
            Console.Clear();
            Console.WriteLine($"! {notification}");
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }

        protected bool ProcessCommandInvariant(Func<string, bool> action, string parameters) => action(parameters) 
            || CommandLanguageExtensions.IsOtherLanguage(parameters) && action(parameters.ToCommandLanguage());
    }
}