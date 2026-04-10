using System;
using System.IO;
using Systems.Sanity.Focus.Infrastructure.FileSynchronization.Git;

namespace Systems.Sanity.Focus;

internal sealed class AppRuntimeOptions
{
    public string? ConfigFilePath { get; init; }

    public string? TestHostPipeName { get; init; }

    public bool IsTestHost => !string.IsNullOrWhiteSpace(TestHostPipeName);

    public bool RunVelopackStartup => !IsTestHost;

    public bool RunUpdateChecker => !IsTestHost;

    public GitSynchronizationOptions GitSynchronizationOptions =>
        IsTestHost
            ? GitSynchronizationOptions.Immediate
            : GitSynchronizationOptions.BackgroundDebounced;

    public string ResolveConfigFilePath()
    {
        if (!string.IsNullOrWhiteSpace(ConfigFilePath))
            return Path.GetFullPath(ConfigFilePath);

        var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userDirectory, "focus-config.json");
    }

    public static AppRuntimeOptions Parse(string[] args)
    {
        var options = new AppRuntimeOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--config":
                    if (index + 1 >= args.Length)
                        throw new ArgumentException("Missing value for --config.");

                    options = new AppRuntimeOptions
                    {
                        ConfigFilePath = args[++index],
                        TestHostPipeName = options.TestHostPipeName,
                    };
                    break;
                case "--test-host":
                    if (index + 1 >= args.Length)
                        throw new ArgumentException("Missing value for --test-host.");

                    options = new AppRuntimeOptions
                    {
                        ConfigFilePath = options.ConfigFilePath,
                        TestHostPipeName = args[++index],
                    };
                    break;
                default:
                    throw new ArgumentException($"Unknown argument \"{argument}\".");
            }
        }

        return options;
    }
}
