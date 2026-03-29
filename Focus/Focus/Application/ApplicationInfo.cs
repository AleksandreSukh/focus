using System.Reflection;
using Velopack.Locators;

namespace Systems.Sanity.Focus.Application;

internal static class ApplicationInfo
{
    private const string ApplicationName = "Focus";

    public static string DefaultConsoleTitle => $"{ApplicationName} v{VersionDescription}";

    public static string VersionDescription => TryGetInstalledVersion()
        ?? TryGetAssemblyInformationalVersion()
        ?? TryGetAssemblyVersion()
        ?? "unknown";

    private static string? TryGetInstalledVersion()
    {
        if (!VelopackLocator.IsCurrentSet)
            return null;

        return VelopackLocator.Current.CurrentlyInstalledVersion?.ToFullString();
    }

    private static string? TryGetAssemblyInformationalVersion()
    {
        var informationalVersion = typeof(ApplicationInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informationalVersion))
            return null;

        return informationalVersion.Split('+', 2)[0];
    }

    private static string TryGetAssemblyVersion()
    {
        return typeof(ApplicationInfo).Assembly.GetName().Version?.ToString(3);
    }
}
