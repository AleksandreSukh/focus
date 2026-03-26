#nullable enable

using System;

namespace Systems.Sanity.Focus.Application.Console;

public interface IConsoleAppSession
{
    ICommandLineEditor CommandLineEditor { get; }

    int WindowWidth { get; }

    void SetTitle(string? title);

    void Clear();

    void ClearScrollback();

    void Write(string text);

    void WriteLine(string text);

    ConsoleKeyInfo ReadKey(bool intercept = true);

    void Beep();
}
