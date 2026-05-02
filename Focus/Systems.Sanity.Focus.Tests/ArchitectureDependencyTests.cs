#nullable enable

namespace Systems.Sanity.Focus.Tests;

public class ArchitectureDependencyTests
{
    [Fact]
    public void ApplicationLayer_DoesNotReferencePagesOutsideCompositionAdapters()
    {
        var applicationDirectory = Path.Combine(FindRepositoryRoot(), "Focus", "Focus", "Application");
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(Path.Combine(applicationDirectory, "ApplicationStartup.cs")),
            NormalizePath(Path.Combine(applicationDirectory, "PageNavigator.cs")),
            NormalizePath(Path.Combine(applicationDirectory, "WorkflowInteractions", "ConsoleWorkflowInteractions.cs"))
        };

        var offenders = Directory
            .EnumerateFiles(applicationDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !allowedFiles.Contains(NormalizePath(file)))
            .Where(file => File.ReadAllText(file).Contains("Systems.Sanity.Focus.Pages", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(applicationDirectory, file).Replace('\\', '/'))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Application files must not reference Pages namespaces outside composition adapters: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void DomainLayer_DoesNotReferenceApplicationLayer()
    {
        var domainDirectory = Path.Combine(FindRepositoryRoot(), "Focus", "Focus", "Domain");
        var offenders = Directory
            .EnumerateFiles(domainDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return text.Contains("Systems.Sanity.Focus.Application", StringComparison.Ordinal) ||
                       text.Contains("AppConsole", StringComparison.Ordinal);
            })
            .Select(file => Path.GetRelativePath(domainDirectory, file).Replace('\\', '/'))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Domain files must not reference application console concerns: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void BackgroundMessages_DoNotUseAppConsoleCurrentOutsideConsoleAdapters()
    {
        var sourceDirectory = Path.Combine(FindRepositoryRoot(), "Focus", "Focus");
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizePath(Path.Combine(sourceDirectory, "Program.cs")),
            NormalizePath(Path.Combine(sourceDirectory, "Application", "ApplicationStatusSink.cs"))
        };

        var offenders = Directory
            .EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !allowedFiles.Contains(NormalizePath(file)))
            .Where(file => File.ReadAllText(file).Contains(
                "AppConsole.Current.WriteBackgroundMessage",
                StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/'))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Background messages must go through IApplicationStatusSink outside console adapters: {string.Join(", ", offenders)}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory != null)
        {
            var applicationDirectory = Path.Combine(directory.FullName, "Focus", "Focus", "Application");
            if (Directory.Exists(applicationDirectory))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
