using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Systems.Sanity.Focus
{
    class Program
    {
        const string MapsFolderName = "FocusMaps"; //TODO

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Default;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                //TypeNameHandling = TypeNameHandling.Objects,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var mapsDirectoryPath = GetMapsDirectoryPath();

            var mapsStorage = new MapsStorage(mapsDirectoryPath);
            var startPage = new HomePage(mapsStorage);
            startPage.Show();
        }

        private static string GetMapsDirectoryPath()
        {
            string MapsDirectoryPathFromDataRoot(string s)
            {
                return Path.Combine(s, MapsFolderName);
            }

            var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configFile = Path.Combine(userDirectory, "focus-config.json");

            string applicationDataRoot;
            UserConfig userConfig;
            if (File.Exists(configFile)
                && (userConfig = JsonConvert.DeserializeObject<UserConfig>(File.ReadAllText(configFile))) != null
                && !string.IsNullOrEmpty(userConfig.DataFolder)
                && Directory.Exists(userConfig.DataFolder))
            {
                applicationDataRoot = userConfig.DataFolder;
            }
            else
            {
                do
                {
                    var dataDirUserInput =
                        ReadLine.Read($"{Environment.NewLine}Press \"Enter\" to save data into Documents folder or type directory path if you need to specify the data folder{Environment.NewLine}>");
                    applicationDataRoot = string.IsNullOrWhiteSpace(dataDirUserInput)
                        ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                        : dataDirUserInput;
                } while (!Directory.Exists(applicationDataRoot));

                userConfig = new UserConfig() { DataFolder = applicationDataRoot };
                File.WriteAllText(configFile, JsonConvert.SerializeObject(userConfig, Formatting.Indented));
            }
            return MapsDirectoryPathFromDataRoot(applicationDataRoot);
        }
    }
}
