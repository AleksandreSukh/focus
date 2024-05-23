using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Systems.Sanity.Focus.Infrastructure
{
    internal class ColorfulConsole
    {
        public static readonly HashSet<string> Colors = Enum.GetNames(typeof(ConsoleColor))
            .Reverse()
            .Select(i => i.ToLower())
            .ToHashSet();

        //Hardcoded priority of colors (before we add better priority suggestion method)
        public static readonly HashSet<string> PrimaryColors = new []
        {
            ConsoleColor.Red, 
            ConsoleColor.Green,
            ConsoleColor.Yellow, 
            ConsoleColor.Blue
        }
            .Select(i => i.ToString().ToLower())
            .ToHashSet();

        public const string ColorCommandTerminationTag = "!";

        public const char CommandStartBracket = '[';
        public const char CommandEndBracket = ']';

        public static void WriteLine(string inputString)
        {
            bool commandStarted = false;
            StringBuilder currentCommand = new StringBuilder();
            foreach (var character in inputString)
            {
                if (character == CommandStartBracket && !commandStarted)
                {
                    commandStarted = true;
                }
                else if (character == CommandEndBracket && commandStarted)
                {
                    commandStarted = false;
                    var currentCommandString = currentCommand.ToString().ToLower();
                    if (Colors.Contains(currentCommandString))
                    {
                        var color = Enum.Parse<ConsoleColor>(currentCommandString, true);
                        Console.ForegroundColor = color;
                    }
                    else if (currentCommandString == ColorCommandTerminationTag)
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
