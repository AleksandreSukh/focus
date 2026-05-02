#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Application.Console;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages;
using Velopack;

namespace Systems.Sanity.Focus;

internal static class Program
{
    private static void Main(string[] args)
    {
        AppRuntimeOptions runtimeOptions;
        try
        {
            runtimeOptions = AppRuntimeOptions.Parse(args);
        }
        catch (Exception ex)
        {
            WriteUserMessage(ex.Message);
            return;
        }

        AppConsole.Current = CreateConsoleSession(runtimeOptions);
        ExceptionDiagnostics.Initialize(
            ExceptionDiagnostics.BuildDefaultLogFilePath(),
            WriteUserMessage);
        if (runtimeOptions.RunUpdateChecker)
        {
            _ = ExceptionDiagnostics.RunInBackgroundAsync(
                "checking for updates",
                () => AutoUpdateManager.StartUpdateChecker(),
                WriteUserMessage);
        }

        var startupReady = ExceptionDiagnostics.Guard(
            "starting application",
            () =>
            {
                if (runtimeOptions.RunVelopackStartup)
                    VelopackApp.Build().Run();

                Console.OutputEncoding = System.Text.Encoding.Default;
                JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

                return true;
            },
            message =>
            {
                WriteUserMessage(message);
                return false;
            });
        if (!startupReady)
            return;

        var configFile = runtimeOptions.ResolveConfigFilePath();

        UserConfig? userConfig = ExceptionDiagnostics.Guard<UserConfig?>(
            "loading configuration",
            () => ParseUserConfig(configFile),
            message =>
            {
                WriteUserMessage(message);
                return null;
            });
        if (userConfig == null)
            return;

        ExceptionDiagnostics.Guard(
            "starting application",
            () =>
            {
                CommandLanguageExtensions.Configure(userConfig.Translations);
                var startPage = ApplicationStartup.CreateHomePage(userConfig, runtimeOptions);
                startPage.Show();
            },
            WriteUserMessage);
    }

    private static UserConfig ParseUserConfig(string configFile)
    {
        UserConfig? DeserializeConfigFile()
        {
            if (!File.Exists(configFile))
                return null;

            return JsonConvert.DeserializeObject<UserConfig>(File.ReadAllText(configFile));
        }

        string applicationDataRoot;
        var userConfig = DeserializeConfigFile();
        if (!File.Exists(configFile) ||
            userConfig == null ||
            string.IsNullOrEmpty(userConfig.DataFolder) ||
            !Directory.Exists(userConfig.DataFolder))
        {
            do
            {
                var dataDirUserInput =
                    AppConsole.Current.CommandLineEditor.Read(
                        $"{Environment.NewLine}Press \"Enter\" to save data into Documents folder or type directory path if you need to specify the data folder{Environment.NewLine}>");
                applicationDataRoot = string.IsNullOrWhiteSpace(dataDirUserInput)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : dataDirUserInput;
            } while (!Directory.Exists(applicationDataRoot));

            userConfig = new UserConfig { DataFolder = applicationDataRoot };
            var configDirectory = Path.GetDirectoryName(configFile);
            if (!string.IsNullOrWhiteSpace(configDirectory))
                Directory.CreateDirectory(configDirectory);
            File.WriteAllText(configFile, JsonConvert.SerializeObject(userConfig, Formatting.Indented));
        }

        return userConfig;
    }

    private static IConsoleAppSession CreateConsoleSession(AppRuntimeOptions runtimeOptions)
    {
        if (runtimeOptions.IsTestHost)
        {
            return new TestHostConsoleSession(
                runtimeOptions.TestHostPipeName
                ?? throw new InvalidOperationException("Test host pipe name is required."));
        }

        return new ConsoleAppSession(new ReadLineCommandLineEditor());
    }

    private static void WriteUserMessage(string message)
    {
        try
        {
            AppConsole.Current.WriteBackgroundMessage(message);
        }
        catch
        {
            Console.Error.WriteLine(message);
        }
    }
}
