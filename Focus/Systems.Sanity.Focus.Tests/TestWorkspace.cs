using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), "focus-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootDirectory);

        MapsStorage = new MapsStorage(new UserConfig
        {
            DataFolder = RootDirectory,
            GitRepository = string.Empty
        });

        Directory.CreateDirectory(MapsStorage.UserMindMapsDirectory);
        AppContext = new FocusAppContext(MapsStorage);
    }

    public string RootDirectory { get; }

    public MapsStorage MapsStorage { get; }

    public FocusAppContext AppContext { get; }

    public string SaveMap(string fileName, MindMap map)
    {
        var normalizedFileName = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.json";
        var filePath = Path.Combine(MapsStorage.UserMindMapsDirectory, normalizedFileName);
        MapsStorage.SaveMap(filePath, map);
        return filePath;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootDirectory))
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }
}
