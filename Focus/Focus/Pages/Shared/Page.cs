using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Edit.Dialogs;

namespace Systems.Sanity.Focus.Pages.Shared
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
        protected ConsoleInput GetInput(string prompt = "", string defaultInput = null)
        {
			if (defaultInput != null)
			{
				Console.Write(prompt);
				var input = ReadLineWithEditMode(defaultInput);
				if (input == null)
					return null;

				return new ConsoleInput(input.Trim());
			}

			return new(ReadLine.Read(prompt).Trim());
        }

        protected void Notify(string notification)
        {
            Console.Clear();
            Console.WriteLine($"! {notification}");
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }

        protected bool ProcessCommandInvariant(Func<string, bool> action, string parameters) => action(parameters) 
            || CommandLanguageExtensions.IsOtherLanguage(parameters) && action(parameters.ToCommandLanguage());

        static string ReadLineWithEditMode(string Default)
        {
	        int pos = Console.CursorLeft;
	        Console.Write(Default);
	        ConsoleKeyInfo info;
	        List<char> chars = new List<char>();
	        if (string.IsNullOrEmpty(Default) == false)
	        {
		        chars.AddRange(Default.ToCharArray());
	        }

	        while (true)
	        {
		        info = Console.ReadKey(true);
		        if (info.Key == ConsoleKey.Backspace && Console.CursorLeft > pos)
		        {
			        chars.RemoveAt(chars.Count - 1);
			        Console.CursorLeft -= 1;
			        Console.Write(' ');
			        Console.CursorLeft -= 1;

		        }
		        else if (info.Key == ConsoleKey.Enter) { Console.Write(Environment.NewLine); break; }
		        //Here you need create own checking of symbols
		        else if (!PathHelpers.InvalidFileNameChars.Contains(info.KeyChar))
		        {
			        Console.Write(info.KeyChar);
			        chars.Add(info.KeyChar);
		        }
	        }
	        return new string(chars.ToArray());
        }
	}
}