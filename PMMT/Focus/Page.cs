using System;
using System.Collections.Generic;
using System.Linq;

namespace Systems.Sanity.Focus
{
    public abstract class Page : IAutoCompleteHandler
    {
        public string[] GetSuggestions(string text, int index)
        {
            return GetSuggestionsInner(text, index)
                .Where(i => i.Length > text.Length && i.StartsWith(text))
                .ToArray();
        }

        public char[] Separators { get; set; } = new char[] { ' ' };

        public virtual IEnumerable<string> GetSuggestionsInner(string text, int index)
            => Array.Empty<string>();

        protected Page()
        {
            ReadLine.AutoCompletionHandler = this;
        }

        public abstract void Show();
        protected ConsoleInput GetInput(string prompt = "") =>
            new(ReadLine.Read(prompt));

        protected void Notify(string notificaiton)
        {
            Console.WriteLine($"! {notificaiton}");
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }
    }
}