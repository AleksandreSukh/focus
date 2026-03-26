using Systems.Sanity.Focus.Application.Console;

namespace Systems.Sanity.Focus.Application;

internal static class AppConsole
{
    public static IConsoleAppSession Current { get; set; } =
        new ConsoleAppSession(new ReadLineCommandLineEditor());
}
