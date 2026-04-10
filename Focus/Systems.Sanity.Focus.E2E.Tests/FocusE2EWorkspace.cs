using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.E2E.Tests;

internal sealed class FocusE2EWorkspace : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public FocusE2EWorkspace()
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), "focus-e2e", Guid.NewGuid().ToString("N"));
        HomeDirectory = Path.Combine(RootDirectory, "home");
        DataFolder = Path.Combine(RootDirectory, "data");
        ConfigFilePath = Path.Combine(RootDirectory, "focus-config.json");

        Directory.CreateDirectory(HomeDirectory);
        Directory.CreateDirectory(DataFolder);
    }

    public string RootDirectory { get; }

    public string HomeDirectory { get; }

    public string DataFolder { get; }

    public string ConfigFilePath { get; }

    public string MapsDirectory => Path.Combine(DataFolder, "FocusMaps");

    public UserConfig WriteConfig(string? dataFolder = null, string? gitRepository = null)
    {
        var config = new UserConfig
        {
            DataFolder = dataFolder ?? DataFolder,
            GitRepository = gitRepository ?? string.Empty,
            Translations = null!
        };

        var configDirectory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrWhiteSpace(configDirectory))
            Directory.CreateDirectory(configDirectory);

        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, JsonOptions));
        return config;
    }

    public void Dispose()
    {
        TestDirectoryCleaner.DeleteDirectory(RootDirectory);
    }
}
