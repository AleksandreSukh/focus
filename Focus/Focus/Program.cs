#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;
using Systems.Sanity.Focus.Pages;
using Velopack;

namespace Systems.Sanity.Focus;

internal static class Program
{
    private static void Main(string[] args)
    {
        Task.Run(AutoUpdateManager.StartUpdateChecker);
        VelopackApp.Build().Run();

        Console.OutputEncoding = System.Text.Encoding.Default;
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configFile = Path.Combine(userDirectory, "focus-config.json");

        var userConfig = ParseUserConfig(configFile);
        CommandLanguageExtensions.Configure(userConfig.Translations);
        var startPage = ApplicationStartup.CreateHomePage(userConfig);
        startPage.Show();
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
                    ReadLine.Read(
                        $"{Environment.NewLine}Press \"Enter\" to save data into Documents folder or type directory path if you need to specify the data folder{Environment.NewLine}>");
                applicationDataRoot = string.IsNullOrWhiteSpace(dataDirUserInput)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : dataDirUserInput;
            } while (!Directory.Exists(applicationDataRoot));

            userConfig = new UserConfig { DataFolder = applicationDataRoot };
            File.WriteAllText(configFile, JsonConvert.SerializeObject(userConfig, Formatting.Indented));
        }

        return userConfig;
    }
}
