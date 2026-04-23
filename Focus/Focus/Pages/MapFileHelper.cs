using System;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Pages;

public class MapFileHelper
{
    public static string GetFullFilePath(string directory, string fileName, string requiredExtension = null)
    {
        var fileNameExtension = string.IsNullOrWhiteSpace(requiredExtension)
            ? ConfigurationConstants.RequiredFileNameExtension
            : requiredExtension;

        if (!fileName.EndsWith(fileNameExtension, StringComparison.InvariantCultureIgnoreCase))
            fileName += fileNameExtension;

        return Path.Combine(directory, fileName);
    }

    public static string SanitizeFileName(string fileName, string fallbackFileName = "untitled")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return fallbackFileName;

        fileName = NodeDisplayHelper.GetSingleLinePreview(fileName);
        fileName = PlainTextInlineFormatter.ToPlainText(fileName);
        if (string.IsNullOrWhiteSpace(fileName))
            return fallbackFileName;

        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitizedFileName = new string(fileName
                .Select(c => invalidChars.Contains(c) ? '_' : c)
                .ToArray())
            .Trim()
            .TrimEnd('.');

        return string.IsNullOrWhiteSpace(sanitizedFileName)
            ? fallbackFileName
            : sanitizedFileName;
    }
}
