using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Systems.Sanity.Focus.Infrastructure
{
    internal class ColorfulConsole
    {
        public static readonly HashSet<string> Colors = Enum.GetNames(typeof(ConsoleColor)).Reverse()
            .Select(i => i.ToLower())
            .ToHashSet();

        public static readonly IEnumerable<string> ColorCommands = new[] { "!" }.Union(Colors).Select(c => $"[{c}]");

        public static void WriteLine(string inputString)
        {
            bool commandStarted = false;
            StringBuilder currentCommand = new StringBuilder();
            foreach (var character in inputString)
            {
                if (character == '[' && !commandStarted)
                {
                    commandStarted = true;
                }
                else if (character == ']' && commandStarted)
                {
                    commandStarted = false;
                    var currentCommandString = currentCommand.ToString().ToLower();
                    if (Colors.Contains(currentCommandString))
                    {
                        var color = Enum.Parse<ConsoleColor>(currentCommandString, true);
                        Console.ForegroundColor = color;
                    }
                    else if (currentCommandString == "!")
                    {
                        Console.ForegroundColor = ConsoleWrapper.DefaultColor;
                    }
                    currentCommand.Clear();
                }
                else if (commandStarted)
                {
                    currentCommand.Append(character);
                }
                else
                {
                    Console.Write(character);
                }
            }

            Console.WriteLine();
        }
    }
}
