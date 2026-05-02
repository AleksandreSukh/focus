#nullable enable

namespace Systems.Sanity.Focus.Application;

internal interface IApplicationStatusSink
{
    void WriteBackgroundMessage(string message);

    void SetTitle(string? title);
}

internal sealed class ConsoleApplicationStatusSink : IApplicationStatusSink
{
    public void WriteBackgroundMessage(string message)
    {
        AppConsole.Current.WriteBackgroundMessage(message);
    }

    public void SetTitle(string? title)
    {
        AppConsole.Current.SetTitle(title);
    }
}
