using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Pages;

namespace Systems.Sanity.Focus
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Default;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                //TypeNameHandling = TypeNameHandling.Objects,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

 

            var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configFile = Path.Combine(userDirectory, "focus-config.json");

            var userConfig = ParseUserConfig(configFile);

            var mapsStorage = new MapsStorage(userConfig);
            var startPage = new HomePage(mapsStorage);
            startPage.Show();
        }

        private static UserConfig ParseUserConfig(string configFile)
        {
            UserConfig? DeserializeConfigFile()
            {
                return JsonConvert.DeserializeObject<UserConfig>(File.ReadAllText(configFile));
            }

            string applicationDataRoot;
            UserConfig userConfig;
            if (!File.Exists(configFile) ||
                (userConfig = DeserializeConfigFile()) == null ||
                string.IsNullOrEmpty(userConfig.DataFolder) || !Directory.Exists(userConfig.DataFolder))
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

                userConfig = new UserConfig() { DataFolder = applicationDataRoot };
                File.WriteAllText(configFile, JsonConvert.SerializeObject(userConfig, Formatting.Indented));
            }

            return userConfig;
        }
    }
}
