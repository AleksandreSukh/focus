using System.IO;

namespace Systems.Sanity.Focus.Pages;

public class MapFileHelper
{
    public static string GetFullFilePath(string directory, string fileName)
    {
        //TODO: 
        var fileNameExtension = ConfigurationConstants.RequiredFileNameExtension;
        if (!fileName.EndsWith(fileNameExtension))
            fileName += fileNameExtension;

        var filePath = Path.Combine(directory, fileName);
        return filePath;
    }
}