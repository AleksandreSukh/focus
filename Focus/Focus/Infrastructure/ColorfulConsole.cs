using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Systems.Sanity.Focus.Infrastructure
{
    internal class ColorfulConsole
    {
        private static HashSet<string> Colors = Enum.GetNames(typeof(ConsoleColor))
            .Select(i => i.ToLower())
            .ToHashSet();
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
