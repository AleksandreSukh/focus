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

    public static readonly IReadOnlyDictionary<ConsoleColor, string> ConsoleColorNames =
        Colors.ToDictionary(k => k.Value, v => v.Key);

    public static readonly HashSet<string> ColorCommands = new[] { FormatColorTag(ColorCommandTerminationTag) }
        .Union(ColorTagsToConsoleColorDict.Keys)
        .ToHashSet();

    public static void Write(string inputString)
    {
        foreach (var run in InlineFormatParser.Parse(inputString))
        {
            Console.ForegroundColor = run.ForegroundColor ?? ConsoleWrapper.DefaultColor;
            Console.Write(run.Text);
        }
    }

    public static void WriteLine(string inputString)
    {
        Write(inputString);
        Console.WriteLine();
    }
}
