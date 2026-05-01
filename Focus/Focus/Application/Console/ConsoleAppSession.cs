#nullable enable

using System;
using System.IO;
using SysConsole = System.Console;

namespace Systems.Sanity.Focus.Application.Console;

internal sealed class ConsoleAppSession : IConsoleAppSession
{
    public ConsoleAppSession(ICommandLineEditor commandLineEditor)
    {
        CommandLineEditor = commandLineEditor;
    }

    public ICommandLineEditor CommandLineEditor { get; }

    public int WindowWidth
    {
        get
        {
            try
            {
                return Math.Max(0, SysConsole.WindowWidth - 2);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
            {
                return 120;
            }
        }
    }

    public bool KeyAvailable
    {
        get
        {
            try
            {
                return SysConsole.KeyAvailable;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException)
            {
                return false;
            }
        }
    }

    public void Beep()
    {
        SysConsole.Beep();
    }

    public void Clear()
    {
        SysConsole.Clear();
    }

    public void ClearScrollback()
    {
        if (OperatingSystem.IsWindows())
        {
            SysConsole.Write("\x1b[3J");
        }
    }

    public ConsoleKeyInfo ReadKey(bool intercept = true)
    {
        return SysConsole.ReadKey(intercept);
    }

    public void SetTitle(string? title)
    {
        if (OperatingSystem.IsWindows() && title != null)
        {
            SysConsole.Title = title;
        }
    }

    public void Write(string text)
    {
        SysConsole.Write(text);
    }

    public void WriteLine(string text)
    {
        SysConsole.WriteLine(text);
    }

    public void WriteBackgroundMessage(string text)
    {
        CommandLineEditor.WriteInterleavedMessage(text);
    }
}
