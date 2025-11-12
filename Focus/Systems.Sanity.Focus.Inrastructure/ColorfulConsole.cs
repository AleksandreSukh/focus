using System.Collections.Specialized;
using System.Text;

namespace Systems.Sanity.Focus.Infrastructure;

public class ColorfulConsole
{
    public static readonly Dictionary<string, ConsoleColor> Colors =
        new[]
        {
            ConsoleColor.Red,
            ConsoleColor.Green,
            ConsoleColor.Yellow,
            ConsoleColor.Blue
        }
        .Union(Enum.GetValues<ConsoleColor>().Reverse())
        .ToDictionary(i => i.ToString().ToLower(), v => v);

    public const string ColorCommandTerminationTag = "!";

    public const char CommandStartBracket = '[';
    public const char CommandEndBracket = ']';

    private static string FormatColorTag(string c) => $"{CommandStartBracket}{c}{CommandEndBracket}";

    public static readonly Dictionary<string, ConsoleColor> ColorTagsToConsoleColorDict =
        Colors.ToDictionary(k => FormatColorTag(k.Key),
            v => v.Value);

    public static readonly HashSet<string> ColorCommands = new[] { FormatColorTag(ColorCommandTerminationTag) }
        .Union(ColorTagsToConsoleColorDict.Keys)
        .ToHashSet();

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
                if (Colors.TryGetValue(currentCommandString, out ConsoleColor color))
                {
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